import argparse
import csv
import hashlib
import json
import mimetypes
import posixpath
import re
import shutil
import unicodedata
import zipfile
from collections import defaultdict
from pathlib import Path
from xml.etree import ElementTree as ET

NS = {
    "main": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
    "rel": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
    "pkg": "http://schemas.openxmlformats.org/package/2006/relationships",
    "xdr": "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing",
    "a": "http://schemas.openxmlformats.org/drawingml/2006/main",
    "rd": "http://schemas.microsoft.com/office/spreadsheetml/2017/richdata",
    "rdr": "http://schemas.microsoft.com/office/spreadsheetml/2022/richvaluerel",
}


def normalize(value):
    value = unicodedata.normalize("NFKC", str(value or ""))
    return re.sub(r"\s+", " ", value).strip().upper()


def normalize_target(base, target):
    if target.startswith("/"):
        return target.lstrip("/")
    return posixpath.normpath(posixpath.join(posixpath.dirname(base), target))


def relationships_name(part):
    return posixpath.join(posixpath.dirname(part), "_rels", posixpath.basename(part) + ".rels")


def relationships(zf, part):
    name = relationships_name(part)
    if name not in zf.namelist():
        return {}
    root = ET.fromstring(zf.read(name))
    return {
        rel.attrib["Id"]: normalize_target(part, rel.attrib["Target"])
        for rel in root.findall("pkg:Relationship", NS)
    }


def shared_strings(zf):
    if "xl/sharedStrings.xml" not in zf.namelist():
        return []
    root = ET.fromstring(zf.read("xl/sharedStrings.xml"))
    return ["".join(t.text or "" for t in item.findall(".//main:t", NS)) for item in root.findall("main:si", NS)]


def cell_values(sheet_root, shared):
    result = {}
    for cell in sheet_root.findall(".//main:c", NS):
        ref = cell.attrib.get("r", "")
        kind = cell.attrib.get("t")
        value_node = cell.find("main:v", NS)
        inline = cell.find("main:is", NS)
        value = ""
        if inline is not None:
            value = "".join(t.text or "" for t in inline.findall(".//main:t", NS))
        elif value_node is not None:
            value = value_node.text or ""
            if kind == "s" and value.isdigit():
                index = int(value)
                value = shared[index] if index < len(shared) else value
        if value:
            result[ref] = value
    return result


def cell_coordinates(ref):
    match = re.fullmatch(r"([A-Z]+)(\d+)", ref.upper())
    letters, row_text = match.groups()
    column = 0
    for character in letters:
        column = column * 26 + ord(character) - 64
    return int(row_text), column


def containing_merge(sheet_root, ref):
    row, column = cell_coordinates(ref)
    for merge in sheet_root.findall("main:mergeCells/main:mergeCell", NS):
        merge_ref = merge.attrib["ref"]
        parts = merge_ref.split(":")
        start_row, start_column = cell_coordinates(parts[0])
        end_row, end_column = cell_coordinates(parts[-1])
        if start_row <= row <= end_row and start_column <= column <= end_column:
            return merge_ref, start_row, end_row
    return ref, row, row


def products_in_rows(cells, start_row, end_row):
    result = []
    for row in range(start_row, end_row + 1):
        code = cells.get(f"B{row}", "").strip()
        brand = cells.get(f"C{row}", "").strip()
        if code:
            result.append({
                "row": row,
                "code": code,
                "brand": brand,
                "name": cells.get(f"A{row}", "").strip(),
            })
    return result


def image_extension(name, content):
    suffix = Path(name).suffix.lower()
    if suffix in {".jpg", ".jpeg"}:
        return ".jpg"
    if suffix in {".png", ".webp", ".gif", ".bmp", ".tif", ".tiff"}:
        return suffix
    guessed = mimetypes.guess_extension(mimetypes.guess_type(name)[0] or "")
    return guessed or ".bin"


def build_media_names(zf, media_parts, output_dir):
    output_dir.mkdir(parents=True, exist_ok=True)
    names = {}
    hashes = {}
    for media_part in sorted(media_parts):
        content = zf.read(media_part)
        digest = hashlib.sha256(content).hexdigest()
        extension = image_extension(media_part, content)
        filename = f"catalogador-v2-{digest[:16]}{extension}"
        names[media_part] = filename
        hashes[media_part] = digest
        destination = output_dir / filename
        if not destination.exists():
            destination.write_bytes(content)
    return names, hashes


def parse_workbook(workbook_path, image_output):
    associations = []
    with zipfile.ZipFile(workbook_path) as zf:
        media_parts = [name for name in zf.namelist() if name.startswith("xl/media/") and not name.endswith("/")]
        media_names, media_hashes = build_media_names(zf, media_parts, image_output)
        shared = shared_strings(zf)

        metadata = ET.fromstring(zf.read("xl/metadata.xml"))
        metadata_values = [int(rc.attrib["v"]) for rc in metadata.findall("main:valueMetadata/main:bk/main:rc", NS)]
        rich_values = ET.fromstring(zf.read("xl/richData/rdrichvalue.xml"))
        rich_value_rel_indexes = [int(rv.findtext("rd:v", default="0", namespaces=NS)) for rv in rich_values.findall("rd:rv", NS)]
        rich_rel_root = ET.fromstring(zf.read("xl/richData/richValueRel.xml"))
        rich_rel_ids = [rel.attrib[f"{{{NS['rel']}}}id"] for rel in rich_rel_root.findall("rdr:rel", NS)]
        rich_rel_targets = relationships(zf, "xl/richData/richValueRel.xml")

        workbook_part = "xl/workbook.xml"
        workbook = ET.fromstring(zf.read(workbook_part))
        workbook_rels = relationships(zf, workbook_part)

        for sheet in workbook.findall("main:sheets/main:sheet", NS):
            sheet_name = sheet.attrib["name"]
            sheet_rid = sheet.attrib[f"{{{NS['rel']}}}id"]
            sheet_part = workbook_rels[sheet_rid]
            sheet_root = ET.fromstring(zf.read(sheet_part))
            cells = cell_values(sheet_root, shared)

            for cell in sheet_root.findall(".//main:c", NS):
                vm = cell.attrib.get("vm")
                if not vm:
                    continue
                metadata_index = int(vm) - 1
                rich_value_index = metadata_values[metadata_index]
                rel_index = rich_value_rel_indexes[rich_value_index]
                rel_id = rich_rel_ids[rel_index]
                media_part = rich_rel_targets[rel_id]
                cell_ref = cell.attrib["r"]
                image_range, start_row, end_row = containing_merge(sheet_root, cell_ref)
                for product in products_in_rows(cells, start_row, end_row):
                    associations.append({
                        **product,
                        "sheet": sheet_name,
                        "source": image_range,
                        "media_part": media_part,
                        "image": media_names[media_part],
                        "hash": media_hashes[media_part],
                    })

            drawing_node = sheet_root.find("main:drawing", NS)
            if drawing_node is None:
                continue
            sheet_rels = relationships(zf, sheet_part)
            drawing_rid = drawing_node.attrib[f"{{{NS['rel']}}}id"]
            drawing_part = sheet_rels[drawing_rid]
            drawing_root = ET.fromstring(zf.read(drawing_part))
            drawing_rels = relationships(zf, drawing_part)
            anchors = list(drawing_root.findall("xdr:oneCellAnchor", NS)) + list(drawing_root.findall("xdr:twoCellAnchor", NS))
            for anchor in anchors:
                marker = anchor.find("xdr:from", NS)
                picture = anchor.find("xdr:pic", NS)
                if marker is None or picture is None:
                    continue
                start_row = int(marker.findtext("xdr:row", default="0", namespaces=NS)) + 1
                end_marker = anchor.find("xdr:to", NS)
                end_row = int(end_marker.findtext("xdr:row", default=str(start_row - 1), namespaces=NS)) + 1 if end_marker is not None else start_row
                blip = picture.find(".//a:blip", NS)
                rel_id = blip.attrib.get(f"{{{NS['rel']}}}embed") if blip is not None else None
                media_part = drawing_rels.get(rel_id, "")
                if not media_part:
                    continue
                source = f"filas {start_row}:{end_row}"
                for product in products_in_rows(cells, start_row, end_row):
                    associations.append({
                        **product,
                        "sheet": sheet_name,
                        "source": source,
                        "media_part": media_part,
                        "image": media_names[media_part],
                        "hash": media_hashes[media_part],
                    })

    return associations, media_names


def create_mapping(associations):
    groups = defaultdict(list)
    for item in associations:
        groups[(normalize(item["code"]), normalize(item["brand"]))].append(item)

    rows = []
    for _, items in sorted(groups.items()):
        images = sorted({item["image"] for item in items})
        status = "LISTO" if len(images) == 1 else "AMBIGUO"
        first = items[0]
        for image in images:
            source_items = [item for item in items if item["image"] == image]
            rows.append({
                "Estado": status,
                "CodigoCatalogo": first["code"],
                "Marca": first["brand"],
                "Imagen": image,
                "HojaExcel": " | ".join(sorted({item["sheet"] for item in source_items})),
                "FilasExcel": " | ".join(sorted({item["source"] for item in source_items})),
                "NombreReferencia": first["name"],
            })
    return rows


def write_csv(path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    columns = ["Estado", "CodigoCatalogo", "Marca", "Imagen", "HojaExcel", "FilasExcel", "NombreReferencia"]
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns, delimiter=";")
        writer.writeheader()
        writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("workbook", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    images = args.output / "imagenes"
    mapping_file = args.output / "MAPEO-IMAGENES-CATALOGADOR.csv"
    if args.output.exists():
        shutil.rmtree(args.output)
    associations, media_names = parse_workbook(args.workbook, images)
    mapping = create_mapping(associations)
    write_csv(mapping_file, mapping)
    summary = {
        "archivos_en_excel": len(media_names),
        "archivos_extraidos_sin_duplicados": len(set(media_names.values())),
        "asociaciones_producto_imagen": len(associations),
        "claves_codigo_marca": len({(normalize(x["code"]), normalize(x["brand"])) for x in associations}),
        "filas_listas": sum(row["Estado"] == "LISTO" for row in mapping),
        "claves_ambiguas": len({(normalize(row["CodigoCatalogo"]), normalize(row["Marca"])) for row in mapping if row["Estado"] == "AMBIGUO"}),
        "filas_ambiguas": sum(row["Estado"] == "AMBIGUO" for row in mapping),
    }
    (args.output / "RESUMEN.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()

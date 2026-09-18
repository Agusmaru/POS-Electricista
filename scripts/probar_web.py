"""Pruebas funcionales HTTP sobre la instancia local, usando usuarios de demo.
No depende de Selenium ni de librerías de red externas.
"""
import json,re,html,urllib.request,urllib.parse,urllib.error,http.cookiejar,io
from pathlib import Path
from pypdf import PdfReader

root=Path(__file__).resolve().parents[1]
access_file=root/'datos/accesos-test.json'
if access_file.exists():
 keys=json.loads(access_file.read_text(encoding='utf-8-sig'))
else:
 access=(root/'ACCESOS-LOCALES.txt').read_text(encoding='utf-8-sig')
 passwords=re.findall(r'Contraseña: (.+)',access)
 keys={'admin':passwords[0],'asesor':passwords[1],'presupuesto':1}
base='http://localhost:5088/'
results=[]
class Client:
 def __init__(self):self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
 def request(self,path,data=None):
  req=urllib.request.Request(urllib.parse.urljoin(base,path),data=urllib.parse.urlencode(data).encode() if data is not None else None)
  try:r=self.opener.open(req,timeout=60)
  except urllib.error.HTTPError as e:r=e
  raw=r.read();return r.status,r.geturl(),raw.decode('utf-8',errors='replace'),raw
 def get(self,path):return self.request(path)
 def post(self,path,data,page=None):
  if page is None:page=self.get(path)[2]
  data=dict(data);data['csrf']=field(page,'csrf');return self.request(path,data)
 def login(self,role):
  r=self.post('Login',{'email':role+'@electricistas.local','password':keys[role]})
  check(r[0]==200 and 'Catalogo' in r[1],'Login '+role)
  return r
def field(page,name):
 from html.parser import HTMLParser
 class Parser(HTMLParser):
  value=None
  def handle_starttag(self,tag,attrs):
   a=dict(attrs)
   if tag=='input' and a.get('name')==name and self.value is None:self.value=a.get('value','')
 p=Parser();p.feed(page)
 assert p.value is not None,'Campo no encontrado: '+name
 return p.value
def check(ok,name):
 assert ok,name
 results.append(name);print('OK '+name,flush=True)
def content(r):return html.unescape(r[2])

anon=Client();check('Login' in anon.get('Catalogo')[1],'Catálogo requiere autenticación')
advisor=Client();advisor.login('asesor')
admin=Client();admin.login('admin')
check(advisor.get('EditarProducto')[0]==403,'Asesor no puede administrar productos')
check(advisor.get('UsuariosCatalogo')[0]==403,'Asesor no puede crear usuarios')
check(advisor.get('AgregarProducto.aspx')[0]==404,'Pantallas anteriores del POS bloqueadas')
check('1677 materiales' in content(advisor.get('Catalogo')),'Catálogo completo de 1677 productos')
check('UNIP-150' in content(advisor.get('Catalogo?q=UNIP-150&marca=Argenplas')),'Filtros por código y marca')
check('No hay productos' in content(advisor.get('Catalogo?tipo=INEXISTENTE')),'Filtro por tipo')
r=advisor.get('DescargarPdf?id='+str(keys['presupuesto']))
check(r[0]==200 and r[3].startswith(b'%PDF'),'PDF generado por la aplicación')
pdf=PdfReader(io.BytesIO(r[3]));text='\n'.join(x.extract_text() for x in pdf.pages)
check('499.900,00' in text and 'Precios ficticios' in text and 'confirmarse' in text,'PDF contiene total, marca de prueba y aclaración')
check(len(pdf.pages)==2 and text.count('UNIP-150')==1,'PDF paginado sin duplicar renglones')
(root/'lista de compra de test.pdf').write_bytes(r[3])

page=advisor.get('Presupuestos')[2]
check(advisor.request('Presupuestos',{'accion':'nuevo','nombre':'No debe crearse','csrf':'incorrecto'})[0]==400,'Protección CSRF')
r=advisor.post('Presupuestos',{'accion':'nuevo','nombre':'QA automatizada','local':'Local de prueba'},page)
check(r[0]==200 and 'Presupuesto?id=' in r[1],'Crear presupuesto')
qid=int(urllib.parse.parse_qs(urllib.parse.urlparse(r[1]).query)['id'][0]);path='Presupuesto?id='+str(qid)
empty=advisor.get('DescargarPdf?id='+str(qid));check(empty[0]==400,'No exporta presupuestos vacíos')
page=advisor.get(path+'&buscar=UNIP-150')[2];pid=field(page,'producto');rev=field(page,'revision')
r=advisor.post(path,{'accion':'agregar','revision':rev,'producto':pid,'cantidad':'2.5','precio':'100.01','nota':'Prueba de fracción'},page)
check(r[0]==200 and '$ 250,03' in content(r),'Cantidad decimal y redondeo monetario')
check(advisor.post(path,{'accion':'agregar','revision':rev,'producto':pid,'cantidad':'1','precio':'1'},r[2])[0]==400,'Rechaza edición concurrente obsoleta')
page=advisor.get(path)[2];rev=field(page,'revision')
check(advisor.post(path,{'accion':'agregar','revision':rev,'producto':pid,'cantidad':'0','precio':'1'},page)[0]==400,'Rechaza cantidad cero')
check(advisor.post(path,{'accion':'agregar','revision':rev,'producto':pid,'cantidad':'1','precio':'-5'},page)[0]==400,'Rechaza precio negativo')
r=advisor.post(path,{'accion':'agregar','revision':rev,'producto':pid,'cantidad':'1','precio':'','nota':'Sin precio'},page)
check(r[0]==200 and 'no representa el costo completo' in content(r),'Precio faltante no se confunde con cero')
missing=advisor.get('DescargarPdf?id='+str(qid));mt='\n'.join(p.extract_text() for p in PdfReader(io.BytesIO(missing[3])).pages)
check('SUBTOTAL CON PRECIO' in mt and 'Pendiente' in mt,'PDF parcial identifica importes pendientes')
page=r[2];item=field(page,'item');rev=field(page,'revision')
r=advisor.post(path,{'accion':'item','revision':rev,'item':item,'cantidad':'3','precio':'100.01','nota':'Editado'},page)
check(r[0]==200 and '$ 300,03' in content(r),'Editar renglón guardado')
page=r[2];r=advisor.post(path,{'accion':'quitar','revision':field(page,'revision'),'item':field(page,'item')},page)
check(r[0]==200 and '1 renglones' in content(r),'Quitar renglón')
r=advisor.post('Presupuestos',{'accion':'duplicar','id':qid})
copyid=int(urllib.parse.parse_qs(urllib.parse.urlparse(r[1]).query)['id'][0])
check('(copia)' in content(r) and '1 renglones' in content(r),'Duplicar presupuesto y conservar materiales')
other=admin.post('Presupuestos',{'accion':'nuevo','nombre':'QA privada admin','local':'Prueba'})
privateid=int(urllib.parse.parse_qs(urllib.parse.urlparse(other[1]).query)['id'][0])
check(advisor.get('Presupuesto?id='+str(privateid))[0]==404,'Aislamiento de presupuestos entre usuarios')
check(advisor.get('DescargarPdf?id='+str(privateid))[0]==404,'Aislamiento de descarga PDF')

# Alta y modificación administrativas; solamente sobre un artículo creado para QA.
r=admin.post('EditarProducto',{'nombre':'QA producto temporal','marca':'QA','categoria':'Prueba','codigo':'QA-TEMP','local':'LOCAL-QA','tipo':'Tipo QA','unidad':'unidad','precio':'12.34','descripcion':'Producto de prueba temporal','activo':'1'})
check(r[0]==200,'Administrador crea producto')
page=admin.get('Catalogo?q=QA-TEMP')[2];product_id=re.search(r'EditarProducto\?id=(\d+)',page)[1]
r=admin.post('EditarProducto?id='+product_id,{'nombre':'QA producto temporal','marca':'QA','categoria':'Prueba','codigo':'QA-TEMP','local':'LOCAL-QA','tipo':'Tipo QA','unidad':'unidad','precio':'20','descripcion':'Producto de prueba temporal'})
check(r[0]==200 and 'No hay productos' in content(advisor.get('Catalogo?q=QA-TEMP')),'Baja lógica oculta producto al asesor')

for c,ident in ((advisor,qid),(advisor,copyid),(admin,privateid)):
 page=c.get('Presupuesto?id='+str(ident))[2]
 r=c.post('Presupuestos',{'accion':'eliminar','id':ident,'revision':field(page,'revision')})
 check(r[0]==200 and c.get('Presupuesto?id='+str(ident))[0]==404,'Eliminar presupuesto QA '+str(ident))
r=advisor.post('Cuenta',{'accion':'salir'});check('Login' in r[1] and 'Login' in advisor.get('Catalogo')[1],'Cerrar sesión invalida acceso')
(root/'datos/pruebas-web.json').write_text(json.dumps({'resultado':'OK','pruebas':results,'producto_qa':int(product_id),'presupuestos_qa':[qid,copyid,privateid]},ensure_ascii=False,indent=2),encoding='utf-8')
print('TOTAL',len(results),flush=True)

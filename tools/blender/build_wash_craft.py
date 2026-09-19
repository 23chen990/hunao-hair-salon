"""Deterministic Blender-authored salon slice. Run with Blender --background --python.
All coordinates below are Unity world axes/metres; U converts to Blender Z-up.
No raster artwork, baked ground shadows, or changes to approved source assets.
"""
import bpy, math, random, pathlib, json, sys
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[2]
OUT=ROOT/'unity-hair-salon/Assets/Resources/Models/WashCraft'
SOURCE=ROOT/'assets/source/wash-craft'
rng=random.Random(831)
PALETTE={
 'Porcelain':('#eee5cf',.24), 'PorcelainInner':('#d8d9c8',.28),
 'Teal':('#397e79',.62), 'TealLight':('#58978c',.65), 'TealEdge':('#2a605d',.64),
 'TealBase':('#234d4a',.68), 'Seam':('#306d66',.70),
 'Oak':('#a97a49',.72), 'OakLight':('#c39660',.74), 'OakDark':('#715337',.78),
 'Brass':('#bda274',.32), 'Metal':('#3b4342',.36), 'Chrome':('#b9c6c0',.18),
 'Plaster':('#d6c6ae',.85), 'Grout':('#608176',.86), 'Stone':('#c1bba7',.84),
 'BottleLilac':('#a092b9',.48),'BottleAmber':('#c9965c',.48),'BottleBlue':('#729fa1',.5),
 'BottlePink':('#c2949d',.52), 'Label':('#ede2c7',.8),
 'Leaf':('#6c8747',.86),'LeafLight':('#93a257',.88),'LeafDark':('#435f3a',.85),'Soil':('#413b2c',1),
}
MATS={}
# Unity's FBX importer converts handedness by reflecting X. Bake that conversion here.
def U(p):return (-p[0],-p[2],p[1])
def material(name):
 if name in MATS:return MATS[name]
 color,rough=PALETTE[name]
 rgb=[int(color[i:i+2],16)/255 for i in (1,3,5)]
 m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*rgb,1);bs.inputs['Roughness'].default_value=rough
 m['salon_roughness']=rough
 MATS[name]=m;return m

def cube(name,p,s,mat,bevel=0,rx=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=U(p));o=bpy.context.object;o.name=name;o.dimensions=(s[0],s[2],s[1])
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if rx:o.rotation_euler.x=math.radians(rx)
 o.data.materials.append(material(mat))
 if bevel:
  mod=o.modifiers.new('Crafted softened edges','BEVEL');mod.width=bevel;mod.segments=2
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o

def cylinder(name,p,radius,depth,mat,vertices=16,radius2=None):
 bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=radius,radius2=radius if radius2 is None else radius2,depth=depth,location=U(p))
 o=bpy.context.object;o.name=name;o.data.materials.append(material(mat));return o

def mesh(name,verts,faces,mat):
 data=bpy.data.meshes.new(name);data.from_pydata([U(v) for v in verts],[],[tuple(reversed(f)) for f in faces]);data.update()
 o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);data.materials.append(material(mat))
 import bmesh
 bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(data);bm.free()
 return o

def rod(name,a,b,r,mat,vertices=10):
 va=Vector(U(a));vb=Vector(U(b));d=vb-va
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(va+vb)/2)
 o=bpy.context.object;o.name=name;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(material(mat));return o

def bottle(x,y,z,i):
 color=['BottleLilac','BottleAmber','BottleBlue','BottlePink'][i%4]
 h=.38+.075*(i%3)
 cube('Shouldered product bottle',(x,y+h*.48,z),(.22,h*.85,.18),color,.035)
 cylinder('Bottle neck',(x,y+h*.96,z),.055,.10,color,10)
 cylinder('Bottle pump',(x,y+h+ .045,z),.04,.075,'Metal',10)
 cube('Pump spout',(x+.045,y+h+.09,z),(.15,.04,.06),'Metal',.012)
 cube('Paper label',(x,y+h*.45,z-.095),(.145,h*.35,.012),'Label',.008)

def plant(p):
 x,y,z=p
 cylinder('Stone planter',(x,y+.33,z),.35,.66,'Stone',8,.44)
 cylinder('Planter rim',(x,y+.66,z),.46,.10,'Porcelain',8)
 cylinder('Soil',(x,y+.70,z),.40,.015,'Soil',12)
 for i in range(12):
  angle=i*2.399;h=.65+(i%4)*.23;r=.38+(.2 if i%3==0 else 0)
  end=(x+math.cos(angle)*r,y+.76+h,z+math.sin(angle)*r)
  start=Vector((x+math.cos(angle)*.13,y+.78+h*.38,z+math.sin(angle)*.13))
  rod('Plant stem',(x,y+.69,z),start,.022,'LeafDark',6)
  tip=Vector(end)+Vector((math.cos(angle)*.2,.12,math.sin(angle)*.2))
  mid=(start+tip)*.5+Vector((0,.10,0));width=.23+(i%3)*.035
  side=Vector((-math.sin(angle)*width,0,math.cos(angle)*width))
  ridge=mid+Vector((0,.07,0)); underside=mid-Vector((0,.025,0))
  mesh('Broad faceted leaf',[start,mid+side,tip,mid-side,ridge,underside],
       [(0,1,4),(1,2,4),(2,3,4),(3,0,4),(1,0,5),(2,1,5),(3,2,5),(0,3,5)],
       ['Leaf','LeafLight','LeafDark'][i%3])

def wash():
 cube('Chamfered stone plinth',(0,.10,0),(2.12,.20,3.04),'Porcelain',.10)
 cube('Inset teal plinth',(0,.24,-.03),(1.94,.16,2.82),'TealBase',.07)
 cube('Sculpted pedestal',(0,.68,-.15),(1.35,.78,2.1),'TealBase',.15)
 cube('Side inset left',(-.687,.72,-.30),(.026,.38,1.26),'TealEdge',.02)
 cube('Side inset right',(.687,.72,-.30),(.026,.38,1.26),'TealEdge',.02)
 cube('Floating upholstery frame',(0,1.14,-.26),(1.76,.25,2.22),'TealEdge',.11,rx=-13)
 for j,(z,h,d) in enumerate([(-1.04,1.29,.54),(-.46,1.44,.60),(.18,1.59,.65)]):
  cube('Upholstered cushion '+str(j),(0,h,z),(1.46,.29,d),'TealLight' if j==1 else 'Teal',.105,rx=-13)
  for side in (-1,1):
   cube('Stitched edge',(side*.685,h+.078,z),(.022,.028,d*.77),'Seam',.01,rx=-13)
 for side in (-1,1):
  rod('Arm support',(side*.81,1.02,-.55),(side*.81,1.61,-.55),.055,'Chrome')
  rod('Arm support',(side*.81,1.05,.34),(side*.81,1.78,.34),.055,'Chrome')
  cube('Cream arm shell',(side*.85,1.72,-.09),(.26,.20,1.22),'Porcelain',.085,rx=-9)
  cube('Arm cushion',(side*.85,1.83,-.09),(.22,.10,1.03),'TealEdge',.045,rx=-9)
 cube('Foot rest',(0,.78,-1.39),(1.33,.12,.25),'Metal',.04)
 for i in range(7):cube('Foot rest groove',(-.5+i/6,.845,-1.39),(.025,.009,.20),'Chrome')
 cube('Basin pedestal',(0,1.01,1.02),(.83,1.38,.79),'TealBase',.12)
 rings=[(.57,.36,1.67),(.80,.51,1.90),(.87,.57,2.07),(.83,.53,2.12),(.69,.43,1.91),(.43,.25,1.78),(.14,.09,1.76)]
 verts=[];n=32
 for rx,rz,h in rings:
  for i in range(n):
   a=i*2*math.pi/n;x=math.cos(a)*rx;z=math.sin(a)*rz
   notch=.13*max(0,1-abs(x)/.32) if z<-.30 and h>2 else 0
   verts.append((x,h-notch,z+1.0))
 faces=[]
 for j in range(len(rings)-1):
  for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 bowl=mesh('Hollow porcelain basin with neck recess',verts,faces,'Porcelain')
 bowl.data.materials.append(material('PorcelainInner'))
 for poly in bowl.data.polygons:
  if poly.index>=3*n:poly.material_index=1
 cylinder('Drain',(0,1.761,1.0),.09,.01,'Chrome',16)
 cylinder('Drain centre',(0,1.769,1.0),.043,.008,'Metal',12)
 rod('Faucet upright',(.49,2.05,1.37),(.49,2.25,1.37),.055,'Chrome')
 rod('Faucet curved spout',(.49,2.25,1.37),(.49,2.25,1.11),.045,'Chrome')
 rod('Water nozzle',(.49,2.25,1.11),(.49,2.17,1.11),.05,'Chrome')
 cylinder('Tap control',(-.49,2.10,1.37),.072,.11,'Chrome',12)
 cube('Neck cushion',(0,1.99,.53),(.48,.12,.19),'TealEdge',.065)

def room():
 # Continuous finish follows the EXISTING room footprint. No station or route moves.
 cube('Floor foundation',(0,-.12,1),(21.4,.22,13.3),'Grout',.06)
 for k in range(9):
  base=(80,123,115);v=k-4
  PALETTE['Tile'+str(k)]=('#%02x%02x%02x'%(base[0]+v*2,base[1]+v*2,base[2]+v*2),.78)
 for row in range(34):
  z=-5.48+row*.38
  for col in range(29):
   x=-10.48+col*.74+(row%2)*.37
   if x+.72>10.5:continue
   mesh('Small staggered glazed tile',[(x,0,z),(x+.724,0,z),(x+.724,0,z+.365),(x,0,z+.365)],[(0,3,2,1)],'Tile'+str(rng.randrange(9)))
 for k in range(7):
  PALETTE['Oak'+str(k)]=('#%02x%02x%02x'%(128+k*5,92+k*4,58+k*3),.78)
 for k in range(5):
  PALETTE['Plaster'+str(k)]=('#%02x%02x%02x'%(209+k*3,194+k*3,171+k*3),.88)
 # Back wall keeps the approved border but lowers the visual cap to reveal the shop.
 cube('Back plaster',(0,1.74,7.43),(21.65,3.48,.24),'Plaster',.03)
 for i in range(50):
  x=-10.57+i*.43
  cube('Oak wainscot panel',(x,.56,7.265),(.422,1.10,.12),'Oak'+str(rng.randrange(7)),.012)
 for i in range(36):
  x=-10.48+i*.60
  cube('Plaster tile',(x,2.20,7.283),(.591,2.18,.035),'Plaster'+str(rng.randrange(5)),.007)
 cube('Back dado rail',(0,1.17,7.20),(21.7,.12,.22),'OakLight',.025)
 cube('Back skirting',(0,.13,7.19),(21.7,.20,.21),'OakDark',.025)
 cube('Back cornice',(0,3.47,7.39),(22,.19,.46),'Porcelain',.04)
 # Right wall remains in the same place; left boundary is a low cutaway wall for visibility.
 for side in (-1,1):
  x=side*10.73;h=3.48 if side==1 else .67
  cube('Side wall',(x,h/2,1),(.24,h,13.0),'Plaster',.02)
  for j in range(30):
   z=-5.22+j*.43
   cube('Side oak panel',(x-side*.16,min(.55,h*.45),z),(.12,min(1.10,h*.85),.422),'Oak'+str(rng.randrange(7)),.01)
  cube('Side cornice',(x,h,1),(.46,.16,13.2),'Porcelain',.035)
 # Only the two existing wash-area shelves are replaced, at their original centres.
 for x in (-8,-4):
  cube('Oak product ledge',(x,2.9,6.75),(3,.20,.65),'Oak',.045)
  cube('Ledge front trim',(x,2.94,6.414),(3.05,.13,.09),'OakLight',.025)
  for side in (-1,1):
   cube('Brass shelf bracket',(x+side*1.14,2.73,6.80),(.075,.32,.43),'Brass',.02)
  for i in range(7):bottle(x-1.13+i*.36,3.01,6.75,i)
 plant((-9.4,0,5.7))

def export(name,build):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 build()
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
 bpy.ops.object.select_all(action='SELECT')
 bpy.context.view_layer.objects.active=next(o for o in bpy.context.scene.objects if o.type=='MESH')
 bpy.ops.object.join();obj=bpy.context.object;obj.name=name
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},add_leaf_bones=False,
  axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=True)
 return {'asset':name,'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'materials':len(obj.data.materials)}
OUT.mkdir(parents=True,exist_ok=True);SOURCE.mkdir(parents=True,exist_ok=True)
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
reports=[export('wash-station',wash),export('room-finish',room)]
(SOURCE/'build-report.json').write_text(json.dumps({'blender':bpy.app.version_string,'assets':reports},indent=2))
print('WASH_CRAFT_MODELS_EXPORTED',reports)

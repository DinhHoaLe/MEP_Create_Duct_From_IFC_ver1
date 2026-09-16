import argparse
import json
from collections import Counter
from pathlib import Path
parser = argparse.ArgumentParser(description='Inspect an ASCII DXF and export dynamic-block fixtures.')
parser.add_argument('input', type=Path, help='Path to the source ASCII DXF')
parser.add_argument('--output-dir', type=Path, required=True, help='Directory for parsed.json and sample-outlines.tsv')
args = parser.parse_args()
p = args.input.resolve()
if not p.is_file():
    parser.error(f'DXF file not found: {p}')
output = args.output_dir.resolve()
if p in (output / 'parsed.json', output / 'sample-outlines.tsv'):
    parser.error('The input path must not be an output file.')
output.mkdir(parents=True, exist_ok=True)
lines = p.read_text(encoding='utf-8', errors='replace').splitlines()
records=[]
for i in range(0,len(lines)-1,2):
    code=int(lines[i].strip()); value=lines[i+1].strip()
    if code==0: records.append({'kind':value,'tags':[]})
    elif records: records[-1]['tags'].append((code,value))
def one(r,c,default=None): return next((v for k,v in r['tags'] if k==c),default)
blocks={}; current=None; section=None; model=[]; objects=[]
for r in records:
    if r['kind']=='SECTION': section=one(r,2)
    elif r['kind']=='ENDSEC': section=None
    elif section=='BLOCKS':
        if r['kind']=='BLOCK':
            current={'header':r,'entities':[]}; blocks[one(r,2)]=current
        elif r['kind']=='ENDBLK': current=None
        elif current is not None: current['entities'].append(r)
    elif section=='ENTITIES': model.append(r)
    elif section=='OBJECTS': objects.append(r)
inserts=[r for r in model if r['kind']=='INSERT']
counts=Counter(one(r,2) for r in inserts)
print('INSERTS',len(inserts),'TYPES',len(counts))
print('LAYERS',Counter(one(r,8) for r in inserts).most_common(15))
anonymous=[]
for name,count in counts.most_common():
    if not name.startswith('*U'): continue
    b=blocks[name]
    anonymous.append({'name':name,'count':count,'insert':next(r for r in inserts if one(r,2)==name),'block':b})
print('ANONYMOUS',len(anonymous))
objects_by_id={one(r,5):r for r in objects}
def dictionary_child(handle, name):
    tags=objects_by_id.get(handle,{}).get('tags',[])
    for i,(code,value) in enumerate(tags[:-1]):
        if code==3 and value==name: return tags[i+1][1]
def parameter_distance(cache, key):
    r=objects_by_id.get(dictionary_child(cache,key),{})
    tags=r.get('tags',[])
    xs=[float(v) for c,v in tags if c==10]
    ys=[float(v) for c,v in tags if c==20]
    return ((xs[1]-xs[0])**2+(ys[1]-ys[0])**2)**.5 if len(xs)>=2 and len(ys)>=2 else None
fixtures=[]
for item in anonymous:
    r=item['insert']; edges=[e for e in item['block']['entities'] if e['kind']=='LWPOLYLINE']
    if len(edges)!=4: continue
    xs=[float(v) for e in edges for c,v in e['tags'] if c==10]
    ys=[float(v) for e in edges for c,v in e['tags'] if c==20]
    rep=dictionary_child(one(r,360),'AcDbBlockRepresentation')
    app=dictionary_child(rep,'AppDataCache')
    cache=dictionary_child(app,'ACAD_ENHANCEDBLOCKDATA')
    d1=parameter_distance(cache,'1'); d2=parameter_distance(cache,'8')
    if d1 is None or d2 is None: continue
    fixtures.append([one(r,5),d1,d2,min(xs),max(xs),min(ys),max(ys),float(one(r,50,'0')),float(one(r,41,'1')),float(one(r,42,'1')),float(one(r,10)),float(one(r,20))])
    print(one(r,5),one(r,8),'Distance=',round(d1,4),round(d2,4),'Outline=',round(max(xs)-min(xs),4),round(max(ys)-min(ys),4),'LocalX=',round(min(xs),4),round(max(xs),4),'Angle=',one(r,50,'0'),'ScaleX=',one(r,41,'1'))
(output/'sample-outlines.tsv').write_text('\n'.join('\t'.join(map(str,row)) for row in fixtures),encoding='utf8')
print('RECTANGLE FIXTURES',len(fixtures))
(output/'parsed.json').write_text(json.dumps({'blocks':blocks,'inserts':inserts,'objects':objects}),encoding='utf8')

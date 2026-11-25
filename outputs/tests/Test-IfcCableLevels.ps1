param([string]$SampleIfc)
$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot/../src/Ifc/IfcSourceReader.cs","$PSScriptRoot/../src/Models/IfcTerminalSource.cs")
$script:checks=0
function Assert($condition,$message) {if(!$condition){throw $message};$script:checks++}
$text=@'
ISO-10303-21;
HEADER;FILE_SCHEMA(('IFC4'));ENDSEC;DATA;
#1=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
#2=IFCUNITASSIGNMENT((#1));
#3=IFCPROJECT('project',$,$,$,$,$,$,$,#2);
#4=IFCCARTESIANPOINT((0.,0.,0.));
#5=IFCAXIS2PLACEMENT3D(#4,$,$);
#6=IFCLOCALPLACEMENT($,#5);
#7=IFCDIRECTION((0.,0.,1.));
#8=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,12000.,300.);
#9=IFCEXTRUDEDAREASOLID(#8,#5,#7,100.);
#10=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#9));
#11=IFCPRODUCTDEFINITIONSHAPE($,$,(#10));
#12=IFCCABLECARRIERSEGMENT('tray',$,'Tray instance',$,$,#6,#11,$,.CABLETRAYSEGMENT.);
#13=IFCPROPERTYSINGLEVALUE('Length',$,IFCLENGTHMEASURE(12000.),$);
#14=IFCPROPERTYSET('set',$,'Pset_FlowSegmentDuctSegment',$,(#13));
#15=IFCRELDEFINESBYPROPERTIES('prop',$,$,$,(#12),#14);
#16=IFCBUILDINGSTOREY('level',$,'L2',$,$,#6,$,$,.ELEMENT.,4000.);
#17=IFCRELCONTAINEDINSPATIALSTRUCTURE('contain',$,$,$,(#12),#16);
#18=IFCCABLECARRIERSEGMENTTYPE('type',$,'Cable Tray with Fittings:Default',$,$,$,$,$,$,.CABLETRAYSEGMENT.);
#19=IFCRELDEFINESBYTYPE('typed',$,$,$,(#12),#18);
ENDSEC;END-ISO-10303-21;
'@
$path=Join-Path ([IO.Path]::GetTempPath()) (([guid]::NewGuid().ToString())+'.ifc')
try {
 [IO.File]::WriteAllText($path,$text)
 $d=[IFCInfo.IfcSourceReader]::Read($path).Products['tray']
 Assert (!$d.GeometryError -and $d.LengthMm -eq 12000 -and $d.WidthMm -eq 300 -and $d.HeightMm -eq 100) 'Tray length/width/height from heightwise extrusion'
 Assert ($d.LevelName -eq 'L2') 'Read IFC containment storey name'
 Assert ($d.TypeName -eq 'Cable Tray with Fittings:Default') 'Read actual IFC type, without instance numeric suffix'
 [IO.File]::WriteAllText($path,$text.Replace('IFCCABLECARRIERSEGMENT(', 'IFCDUCTSEGMENT(').Replace('IFCCABLECARRIERSEGMENTTYPE(', 'IFCDUCTSEGMENTTYPE('))
 $d=[IFCInfo.IfcSourceReader]::Read($path).Products['tray']
 Assert ([bool]$d.GeometryError) 'Generic ambiguous duct axes remain blocked'
 [IO.File]::WriteAllText($path,$text.Replace("IFCLENGTHMEASURE(12000.)","IFCLENGTHMEASURE(100.)"))
 $d=[IFCInfo.IfcSourceReader]::Read($path).Products['tray']
 Assert (!$d.GeometryError -and $d.LengthMm -eq 100 -and $d.WidthMm -eq 12000 -and $d.HeightMm -eq 300) 'A vertical longitudinal extrusion must not use gravity as its section height'
 if($SampleIfc) {
  $reader=[IFCInfo.IfcSourceReader]::Read($SampleIfc)
  foreach($id in @('3V66APSd56hQuBRubZRs_u','3V66APSd56hQuBRubZRsz$','3V66APSd56hQuBRubZRsyq')) {
   $d=$reader.Products[$id]
   Assert (!$d.GeometryError -and [Math]::Abs($d.WidthMm-304.8) -lt .001 -and [Math]::Abs($d.HeightMm-101.6) -lt .001) "Sample tray dimensions: $id"
  }
 }
} finally {Remove-Item -LiteralPath $path}
Write-Output "PASS: $script:checks IFC cable/level checks"

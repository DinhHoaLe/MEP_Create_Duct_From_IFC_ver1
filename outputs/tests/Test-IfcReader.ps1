param([string]$CadMepFile)
$ErrorActionPreference = 'Stop'
Add-Type -Path @("$PSScriptRoot/../src/Ifc/IfcSourceReader.cs", "$PSScriptRoot/../src/Models/IfcTerminalSource.cs")
$script:checks = 0
function Assert($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Read-Sample([string]$text) {
    $path = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString() + '.ifc')
    try {
        [IO.File]::WriteAllText($path, $text)
        return [IFCInfo.IfcSourceReader]::Read($path)
    } finally { Remove-Item -LiteralPath $path }
}
# Minimal reader fixtures, not full exchange models for Revit import.
$sample = @'
ISO-10303-21;
HEADER;
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('p',$,$,$,$,$,$,$,#2);
#2=IFCUNITASSIGNMENT((#3));
#3=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
#4=IFCDUCTSEGMENT('duct',$,'Duct',$,$,#5,#8,$,.RIGIDSEGMENT.);
#5=IFCLOCALPLACEMENT($,#6);
#6=IFCAXIS2PLACEMENT3D(#7,$,$);
#7=IFCCARTESIANPOINT((0.,0.,0.));
#8=IFCPRODUCTDEFINITIONSHAPE($,$,(#9));
#9=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#10));
#10=IFCEXTRUDEDAREASOLID(#11,#6,#12,1000.);
#11=IFCRECTANGLEPROFILEDEF(.AREA.,$,$,200.,100.);
#12=IFCDIRECTION((0.,0.,1.));
#20=IFCDISTRIBUTIONSYSTEM('sys',$,'Supply Air',$,$,$,.VENTILATION.);
#21=IFCRELASSIGNSTOGROUP('rel',$,$,$,(#4),$,#20);
#30=IFCMAPPEDITEM(#31,#32);
#31=IFCREPRESENTATIONMAP(#6,#33);
#32=IFCCARTESIANTRANSFORMATIONOPERATOR3DNONUNIFORM($,$,#7,2.,$,3.,4.);
#33=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#10));
ENDSEC;
END-ISO-10303-21;
'@
$reader = Read-Sample $sample
$d = $reader.Ducts['duct']
Assert (!$d.GeometryError -and $d.WidthMm -eq 200 -and $d.HeightMm -eq 100 -and $d.LengthMm -eq 1000) 'IFC4 direct extrusion'
Assert ($d.SystemName -eq 'Supply Air' -and $d.SystemType -eq 'VENTILATION') 'IFC4 distribution system'
$legacy = $sample.Replace("'IFC4'", "'IFC2X3'")
Assert (!(Read-Sample $legacy).Ducts['duct'].GeometryError) 'IFC2X3 direct extrusion regression'
$mapped = $sample.Replace("#9=IFCSHAPEREPRESENTATION($,'Body','SweptSolid',(#10));", "#9=IFCSHAPEREPRESENTATION($,'Body','MappedRepresentation',(#30));")
$d = (Read-Sample $mapped).Ducts['duct']
Assert (!$d.GeometryError -and $d.WidthMm -eq 400 -and $d.HeightMm -eq 300 -and $d.LengthMm -eq 4000) 'Nonuniform mapping'
$rotated = $mapped.Replace('#31=IFCREPRESENTATIONMAP(#6,#33);', '#31=IFCREPRESENTATIONMAP(#40,#33);').Replace('ENDSEC;' + "`nEND-ISO-10303-21;", "#40=IFCAXIS2PLACEMENT3D(#7,`$,#41);`n#41=IFCDIRECTION((0.,1.,0.));`nENDSEC;`nEND-ISO-10303-21;")
$d = (Read-Sample $rotated).Ducts['duct']
Assert (!$d.GeometryError -and $d.WidthMm -eq 600 -and $d.HeightMm -eq 200 -and $d.LengthMm -eq 4000) 'Mapping origin rotation before nonuniform scale'
$skew = $rotated.Replace('IFCDIRECTION((0.,1.,0.))', 'IFCDIRECTION((1.,1.,0.))')
Assert ([bool](Read-Sample $skew).Ducts['duct'].GeometryError) 'Reject transformed skew section'
$negative = $mapped.Replace('#7,2.,$,3.,4.', '#7,-2.,$,3.,4.')
Assert ([bool](Read-Sample $negative).Ducts['duct'].GeometryError) 'Reject invalid scale'
$nested = $mapped.Replace('#31=IFCREPRESENTATIONMAP(#6,#33);', '#31=IFCREPRESENTATIONMAP(#6,#34);') + "`n#34=IFCSHAPEREPRESENTATION(`$,'Body','MappedRepresentation',(#35));`n#35=IFCMAPPEDITEM(#36,#32);`n#36=IFCREPRESENTATIONMAP(#6,#33);"
$d = (Read-Sample $nested).Ducts['duct']
Assert (!$d.GeometryError -and $d.WidthMm -eq 800 -and $d.HeightMm -eq 900 -and $d.LengthMm -eq 16000) 'Nested mappings'
$round = $mapped.Replace('IFCRECTANGLEPROFILEDEF(.AREA.,$,$,200.,100.)', 'IFCCIRCLEPROFILEDEF(.AREA.,$,$,50.)')
Assert ((Read-Sample $round).Ducts['duct'].GeometryError -like '*oval*') 'Reject elliptical scaling'
$round = $round.Replace('#7,2.,$,3.,4.', '#7,2.,$,2.,4.')
$d = (Read-Sample $round).Ducts['duct']
Assert (!$d.GeometryError -and $d.DiameterMm -eq 200 -and $d.LengthMm -eq 4000) 'Round mapping'
$cycle = $mapped.Replace('#31=IFCREPRESENTATIONMAP(#6,#33);', '#31=IFCREPRESENTATIONMAP(#6,#9);')
Assert ([bool](Read-Sample $cycle).Ducts['duct'].GeometryError) 'Reject cycles'
$multi = $mapped.Replace("'SweptSolid',(#10)", "'SweptSolid',(#10,#10)")
Assert ([bool](Read-Sample $multi).Ducts['duct'].GeometryError) 'Reject multiple mapped solids'
$brep = $sample.Replace('IFCEXTRUDEDAREASOLID(#11,#6,#12,1000.)', 'IFCFACETEDBREP(#6)')
Assert ((Read-Sample $brep).Ducts['duct'].GeometryError -like '*IFCFACETEDBREP*') 'Specific unsupported geometry message'
if ($CadMepFile) {
    $reader = [IFCInfo.IfcSourceReader]::Read($CadMepFile)
    Assert ($reader.Ducts.Count -eq 6) 'CADMEP duct count (exclude five fittings)'
    $lengths = @(9083,18299,30156,39297,58667,97983)
    $ducts = @($reader.Ducts.Values | Sort-Object LengthMm)
    for ($i=0; $i -lt 6; $i++) {
        $d = $ducts[$i]
        Assert (!$d.GeometryError -and [Math]::Abs($d.WidthMm-75) -lt 0.001 -and [Math]::Abs($d.HeightMm-75) -lt 0.001 -and [Math]::Abs($d.LengthMm-$lengths[$i]) -lt 0.001) "CADMEP dimensions: $($d.Name) $($d.GeometryError)"
    }
    $ducts | Format-Table Name, WidthMm, HeightMm, LengthMm, GeometryError
}
Write-Output "PASS: $script:checks assertions"

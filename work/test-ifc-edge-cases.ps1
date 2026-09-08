$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\ifc2024-build\IFCInfo.dll")
$fixture = @'
ISO-10303-21;
HEADER;
FILE_SCHEMA(('IFC2X3'));
ENDSEC;
DATA;
#1=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#2=IFCDIMENSIONALEXPONENTS(1,0,0,0,0,0,0);
#3=IFCMEASUREWITHUNIT(IFCLENGTHMEASURE(0.3048),#1);
#4=IFCCONVERSIONBASEDUNIT(#2,.LENGTHUNIT.,'FOOT',#3);
#5=IFCUNITASSIGNMENT((#4));
#6=IFCPROJECT('Project',$,$,$,$,$,$,$,#5);
#10=IFCCARTESIANPOINT((0.,0.,10.));
#11=IFCDIRECTION((0.,1.,0.));
#12=IFCDIRECTION((1.,0.,0.));
#13=IFCAXIS2PLACEMENT3D(#10,#11,#12);
#14=IFCLOCALPLACEMENT($,#13);
#15=IFCCARTESIANPOINT((0.,2.,3.));
#16=IFCAXIS2PLACEMENT3D(#15,$,$);
#17=IFCLOCALPLACEMENT(#14,#16);
/* quoted separators must not break records */
#20=IFCAIRTERMINALTYPE('Type',$,'Terminal',$,$,$,$,$,$,.NOTDEFINED.);
#30=IFCFLOWTERMINAL('TerminalGuid',$,'Terminal',$,$,#17,$,$);
#31=IFCRELDEFINESBYTYPE('Rel',$,$,$,(#30),#20);
#40=IFCSYSTEM('Sys1',$,'AHU; Bob''s, system',$,'Supply Air');
#41=IFCSYSTEM('Sys2',$,'Z return',$,'Return Air');
#42=IFCRELASSIGNSTOGROUP('R1',$,$,$,(#30),$,#40);
#43=IFCRELASSIGNSTOGROUP('R2',$,$,$,(#30),$,#41);
ENDSEC;
END-ISO-10303-21;
'@
$path = "$PSScriptRoot\ifc-test-fixture.ifc"
[IO.File]::WriteAllText($path,$fixture)
$source = [IFCInfo.IfcSourceReader]::Read($path).Terminals['TerminalGuid']
if ([Math]::Abs($source.ElevationMm - 2438.4) -gt 0.00001) { throw 'Nested rotated placement / foot conversion failed' }
if ($source.SystemName -ne "AHU; Bob's, system; Z return") { throw 'Quoted strings or multiple systems failed' }
if ($source.SystemType -ne 'Supply Air; Return Air') { throw 'System type pairing failed' }
[IO.File]::WriteAllText($path,$fixture.Replace('IFCLOCALPLACEMENT($,#13)','IFCLOCALPLACEMENT(#17,#13)'))
if ($null -ne [IFCInfo.IfcSourceReader]::Read($path).Terminals['TerminalGuid'].ElevationMm) { throw 'Cycle must not return an elevation' }
[IO.File]::WriteAllText($path,$fixture.Replace("'FOOT'","'FOOT'").Replace('IFCUNITASSIGNMENT((#4))','IFCUNITASSIGNMENT(())'))
if ($null -ne [IFCInfo.IfcSourceReader]::Read($path).Terminals['TerminalGuid'].ElevationMm) { throw 'Missing units must not return an elevation' }
Write-Output 'PASS: rotated nested placement, feet to mm, quoted separators, multiple systems, cycles, missing units.'

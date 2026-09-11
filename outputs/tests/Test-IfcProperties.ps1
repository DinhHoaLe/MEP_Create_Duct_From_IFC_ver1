$ErrorActionPreference='Stop'
Add-Type -Path "$PSScriptRoot/../src/Ifc/IfcSourceReader.cs", "$PSScriptRoot/../src/Models/IfcTerminalSource.cs"
$checks=0
function Assert($ok,$label) { if (!$ok) { throw $label }; $script:checks++ }
$sample=@'
ISO-10303-21;
HEADER;
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('p',$,$,$,$,$,$,$,#2);
#2=IFCUNITASSIGNMENT((#3));
#3=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
#4=IFCWALL('wall',$,'Test wall',$,$,#5,$,$,.STANDARD.);
#5=IFCLOCALPLACEMENT($,#6);
#6=IFCAXIS2PLACEMENT3D(#7,$,$);
#7=IFCCARTESIANPOINT((0.,0.,0.));
#10=IFCPROPERTYSET('s',$,'Pset_WallCommon',$,(#11,#12,#13,#14,#15,#16));
#11=IFCPROPERTYSINGLEVALUE('FireRating',$,IFCLABEL('60 min'),$);
#12=IFCPROPERTYSINGLEVALUE('IsExternal',$,IFCBOOLEAN(.T.),$);
#13=IFCPROPERTYSINGLEVALUE('Length',$,IFCLENGTHMEASURE(1200.),#3);
#14=IFCPROPERTYLISTVALUE('Options',$,(IFCLABEL('A,B'),IFCLABEL('O''Brien')),$);
#15=IFCCOMPLEXPROPERTY('Nested',$,'Usage',(#17));
#16=IFCPROPERTYSINGLEVALUE('Empty',$,$,$);
#17=IFCPROPERTYSINGLEVALUE('Code',$,IFCLABEL('\X2\1ED0\X0\ng'),$);
#20=IFCRELDEFINESBYPROPERTIES('r',$,$,$,(#4),#10);
#30=IFCWALLTYPE('t',$,'Type',$,$,(#31),$,$,$,.STANDARD.);
#31=IFCPROPERTYSET('ts',$,'Pset_Type',$,(#32));
#32=IFCPROPERTYSINGLEVALUE('Manufacturer',$,IFCLABEL('Test Company'),$);
#33=IFCRELDEFINESBYTYPE('rt',$,$,$,(#4),#30);
#40=IFCELEMENTQUANTITY('q',$,'Qto_WallBaseQuantities',$,$,(#41));
#41=IFCQUANTITYLENGTH('Height',$,#3,3000.,$);
#42=IFCRELDEFINESBYPROPERTIES('rq',$,$,$,(#4),#40);
#50=IFCPROPERTYSET('extra',$,'Pset_Extra',$,(#51));
#51=IFCPROPERTYBOUNDEDVALUE('Range',$,IFCREAL(10.),IFCREAL(2.),$,IFCREAL(5.));
#52=IFCRELDEFINESBYPROPERTIES('re',$,$,$,(#4),IFCPROPERTYSETDEFINITIONSET((#50)));
ENDSEC;
END-ISO-10303-21;
'@
# STEP uses a single backslash; single-quoted PowerShell strings preserve it.
$sample=$sample.Replace('\\','\')
$path=Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString()+'.ifc')
try {
    foreach ($schema in @('IFC4','IFC2X3')) {
        [IO.File]::WriteAllText($path,$sample.Replace("'IFC4'","'$schema'"))
        $reader=[IFCInfo.IfcSourceReader]::Read($path)
        Assert ($reader.Products.ContainsKey('wall')) "$schema generic product detected"
        $p=$reader.Products['wall'].Properties
        Assert (($p | Where-Object Name -eq FireRating).Value -eq '60 min') 'Single property'
        Assert (($p | Where-Object Name -eq IsExternal).Value -eq '.T.') 'Boolean property'
        Assert (($p | Where-Object Name -eq Length).Unit -eq 'MILLIMETRE') 'Explicit property unit'
        Assert (($p | Where-Object Name -eq Options).Value -eq "A,B; O'Brien") 'List and escaped quotes'
        Assert (($p | Where-Object Name -eq 'Nested.Code').Value -eq ([string][char]0x1ED0+'ng')) 'Nested Unicode property'
        Assert (($p | Where-Object Name -eq Empty).Value -eq '') 'Unset value preserved'
        Assert (($p | Where-Object Name -eq Manufacturer).Scope -eq 'Type') 'Type property association'
        Assert (($p | Where-Object Name -eq Height).SetName -eq 'Qto_WallBaseQuantities') 'Quantity set'
        Assert (($p | Where-Object Name -eq Range).Value.Contains('Setpoint=5.')) 'IFC4 set aggregate and bounded property'
        Assert ($reader.Ducts.Count -eq 0 -and $reader.Terminals.Count -eq 0) 'Generic wall not misclassified as MEP'
    }
} finally { Remove-Item -LiteralPath $path }
Write-Output "$checks IFC property checks passed."

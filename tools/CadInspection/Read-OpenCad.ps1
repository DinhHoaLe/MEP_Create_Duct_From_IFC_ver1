$ErrorActionPreference = 'Stop'
try {
    $taskCad = [Runtime.InteropServices.Marshal]::GetActiveObject('AutoCAD.Application')
    foreach ($taskDoc in $taskCad.Documents) {
        [pscustomobject]@{ Name = $taskDoc.Name; Path = $taskDoc.FullName }
    }
} catch { Write-Output $_.Exception.Message }

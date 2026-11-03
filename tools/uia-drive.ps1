# Drives a running VAK through UI Automation (no mouse position involved).
# Steps are separated by "|":  invoke:<name regex>   settext:<n>:<text> (nth text box, 0-based)   wait:<ms>   shot:<file>
param([string]$Steps, [string]$OutDir)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
$A = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$proc = Get-Process VAK | Select-Object -First 1
$root = $A::RootElement
function Win { $c = New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $proc.Id); $root.FindAll($TS::Children, $c) }
function Find($ctype, $name) {
  $res = @()
  foreach ($w in (Win)) {
    $c1 = New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, $ctype)
    foreach ($e in $w.FindAll($TS::Descendants, $c1)) { if (-not $name -or $e.Current.Name -match $name) { $res += $e } }
  }
  return $res
}
foreach ($step in $Steps.Split('|')) {
  $kind, $rest = $step.Split(':', 2)
  switch ($kind) {
    'invoke' {
      $found = @(Find ([System.Windows.Automation.ControlType]::Button) $rest)
      if ($found.Count -eq 0) { "NOT FOUND: $rest"; continue }
      $found[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 700
    }
    'settext' {
      $n, $text = $rest.Split(':', 2)
      $boxes = @(Find ([System.Windows.Automation.ControlType]::Edit) $null)
      if ($boxes.Count -le [int]$n) { "NO TEXT BOX $n (have $($boxes.Count))"; continue }
      $boxes[[int]$n].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text); Start-Sleep -Milliseconds 200
    }
    'wait' { Start-Sleep -Milliseconds ([int]$rest) }
    'shot' {
      $b = New-Object System.Drawing.Bitmap 1210, 500; $g = [System.Drawing.Graphics]::FromImage($b)
      $g.CopyFromScreen(0, 0, 0, 0, $b.Size); $b.Save("$OutDir\$rest.png"); $b.Dispose()
    }
  }
}
"done"

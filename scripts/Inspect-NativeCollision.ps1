param([string]$TypePattern='^Assets.Scripts.Simulation.Physics.CollisionChecker$', [string]$MethodPattern='^Process$', [string]$GameDirectory='C:/Program Files (x86)/Steam/steamapps/common/BloonsTD6')
$ErrorActionPreference='Stop'
# Local read-only analysis. Output is proprietary disassembly: keep it local.
# Uses installed Cecil/Iced libraries; does not load or invoke the game binary.
$game=$GameDirectory
$expectedHash='CAF18ADDE5B26658F65A8D040413624886FF90BB15C789A29DED4736F6E6689C'
if ((Get-FileHash (Join-Path $game 'GameAssembly.dll') -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Unreviewed game build: reassess the native address map before analysis.'
}
Add-Type -Path "$game/MelonLoader/net6/Mono.Cecil.dll"
Add-Type -Path "$game/MelonLoader/net6/Iced.dll"
$binary=[IO.File]::ReadAllBytes("$game/GameAssembly.dll")
$pe=[BitConverter]::ToInt32($binary,0x3c)
$base=[BitConverter]::ToUInt64($binary,$pe+24+24)
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$game/MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/cpp2il_out/Assembly-CSharp.dll")
function Types($types) {foreach($type in $types){$type;Types $type.NestedTypes}}
$methods=@(foreach($type in (Types $assembly.MainModule.Types)){foreach($method in $type.Methods){$attr=$method.CustomAttributes|Where-Object {$_.AttributeType.Name -eq 'AddressAttribute'};if(!$attr){continue};$fields=@{};foreach($f in $attr.Fields){$fields[$f.Name]=[Convert]::ToInt64($f.Argument.Value,16)};if($fields.Length -gt 0){[pscustomobject]@{Type=$type.FullName;Name=$method.Name;Signature=$method.FullName;Rva=$fields.RVA;Offset=$fields.Offset;Length=$fields.Length}}}})
$map=@{};foreach($method in $methods){$key=[long]$method.Rva;if(!$map.ContainsKey($key)){$map[$key]=@()};$map[$key]+=$method.Signature}
# Address attributes are hints. Validate each selected range against PE sections.
$sectionCount=[BitConverter]::ToUInt16($binary,$pe+6)
$optionalSize=[BitConverter]::ToUInt16($binary,$pe+20)
$sectionStart=$pe+24+$optionalSize
foreach($method in $methods|Where-Object {$_.Type -match $TypePattern -and $_.Name -match $MethodPattern}){
$valid=$false
for($section=0;$section -lt $sectionCount;$section++) {
    $header=$sectionStart+$section*40
    $rva=[BitConverter]::ToUInt32($binary,$header+12)
    $size=[BitConverter]::ToUInt32($binary,$header+16)
    $offset=[BitConverter]::ToUInt32($binary,$header+20)
    $flags=[BitConverter]::ToUInt32($binary,$header+36)
    if (($flags -band 0x20000000) -ne 0 -and $method.Rva -ge $rva -and
        ($method.Rva+$method.Length) -le ($rva+$size) -and
        $method.Offset -eq ($offset+$method.Rva-$rva) -and
        ($method.Offset+$method.Length) -le $binary.Length) { $valid=$true; break }
}
if (!$valid) { throw "Invalid native range: $($method.Signature)" }
"METHOD $($method.Signature) RVA=$('{0:X}' -f $method.Rva) length=$($method.Length)"
$bytes=[byte[]]::new($method.Length);[Array]::Copy($binary,$method.Offset,$bytes,0,$method.Length)
$decoder=[Iced.Intel.Decoder]::Create(64,$bytes,[uint64]($base+$method.Rva),[Iced.Intel.DecoderOptions]::None)
while($decoder.IP -lt ($base+$method.Rva+$method.Length)){$instruction=$decoder.Decode();$annotation='';if($instruction.FlowControl.ToString() -in @('Call','UnconditionalBranch')){$rva=[long]$instruction.NearBranchTarget-[long]$base;if($map.ContainsKey($rva)){$annotation=' ; '+($map[$rva] -join ' | ')}};('{0:X}: {1}{2}' -f ($instruction.IP-$base),$instruction.ToString(),$annotation)}
}
$assembly.Dispose()

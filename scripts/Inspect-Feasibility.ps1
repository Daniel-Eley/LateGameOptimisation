param(
    [string]$GameDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\BloonsTD6'
)

# Read metadata using the installed loader's Cecil library. Do not load or invoke
# BTD6 types, write into the game, or emit a replacement/game assembly.
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameDirectory 'MelonLoader/net6/Mono.Cecil.dll')
$wrapperPath = Join-Path $GameDirectory 'MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($wrapperPath)
try {
    $names = @(
        'Il2CppAssets.Scripts.Simulation.Physics.CollisionChecker',
        'Il2CppAssets.Scripts.Simulation.Physics.CollisionGrid`1',
        'Il2CppAssets.Scripts.Simulation.Towers.Projectiles.Projectile',
        'Il2CppAssets.Scripts.Simulation.Towers.Projectiles.Behaviors.TrackTarget',
        'Il2CppAssets.Scripts.Models.Towers.Projectiles.Behaviors.TrackTargetModel',
        'Il2CppAssets.Scripts.Simulation.Towers.Projectiles.Behaviors.TravelStrait',
        'Il2CppAssets.Scripts.Simulation.Towers.Target'
    )
    foreach ($type in $assembly.MainModule.Types) {
        if ($type.FullName -notin $names) { continue }
        "TYPE $($type.FullName)"
        foreach ($property in $type.Properties) {
            "  PROPERTY $($property.PropertyType.FullName) $($property.Name)"
        }
        foreach ($method in $type.Methods) {
            if ($method.Name -match '^(get_|set_|\.ctor|\.cctor)') { continue }
            "  METHOD $($method.FullName)"
            if ($method.HasBody) {
                $nativeInvoke = @($method.Body.Instructions | Where-Object {
                    $_.Operand -is [Mono.Cecil.MethodReference] -and
                    $_.Operand.Name -eq 'il2cpp_runtime_invoke'
                }).Count -gt 0
                "    forwards_to_native=$nativeInvoke"
            }
        }
        foreach ($nested in $type.NestedTypes) {
            if ($nested.Name -ne 'CollisionState') { continue }
            "  NESTED $($nested.FullName)"
            foreach ($property in $nested.Properties) {
                "    PROPERTY $($property.PropertyType.FullName) $($property.Name)"
            }
        }
    }
} finally { $assembly.Dispose() }
foreach ($path in @($wrapperPath, (Join-Path $GameDirectory 'GameAssembly.dll'))) {
    $hash = Get-FileHash -LiteralPath $path -Algorithm SHA256
    "SHA256 $([IO.Path]::GetFileName($path)) $($hash.Hash)"
}

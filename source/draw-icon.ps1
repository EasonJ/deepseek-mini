$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null
# Hand-drawn 32 x 32 pixel sprite. No resampling or antialiasing.
$sprite = @(
'................................',
'................................',
'................................',
'...........cc...................',
'.......cc..cc..cc...............',
'........cc.cc.cc................',
'.........cccccc.................',
'...........cc...................',
'...........cc..............bb...',
'.......bbbbbbbbbb.........bbbb..',
'.....bbllllllllllbb.......bbbb..',
'....bllllllllllllllb..bb...bbb...',
'...bllhhhhhhllllllllb.bbb.bbb....',
'..bllhhhhllllllllllllbbbbbbb....',
'..bllllllllllllllllllbbbbb......',
'..blllllllllllllllllllbbb.......',
'..bllllwwllllllllllllllbb.......',
'..bllllwoll llllllllllllbb.......'.Replace(' ',''),
'..bllllllllllllllllllllbb.......',
'..blllllllllllllllllllbb........',
'...bllllllbbbblllllllbbb.........',
'...blllllllbblllllllbbb..........',
'....bllccccccccclllbbb...........',
'.....bbccccccccllbbbb............',
'......bbbbbbbbbbbbbb............',
'.........bbbbbbbb...............',
'............bbb.................',
'.............bb.................',
'................................',
'................................',
'................................',
'................................'
)
$palette = @{'.'=[Drawing.Color]::Transparent;'b'=[Drawing.ColorTranslator]::FromHtml('#3155CF');'l'=[Drawing.ColorTranslator]::FromHtml('#527CFF');'h'=[Drawing.ColorTranslator]::FromHtml('#7DABFF');'c'=[Drawing.ColorTranslator]::FromHtml('#9EDCFF');'w'=[Drawing.Color]::White;'o'=[Drawing.ColorTranslator]::FromHtml('#102B64')}
function Make-Bitmap([int]$size) {
 $bmp = New-Object Drawing.Bitmap($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
 for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){
  $sy=[Math]::Min(31,[int][Math]::Floor($y*32.0/$size));$sx=[Math]::Min(31,[int][Math]::Floor($x*32.0/$size))
  $row=$sprite[$sy].PadRight(32,'.');$bmp.SetPixel($x,$y,$palette[[string]$row[$sx]])
 }}
 return ,$bmp
}
$preview=Make-Bitmap 256
$preview.Save((Join-Path $assets 'whale.png'),[Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
$frames=@()
foreach($size in @(16,20,24,32,40,48,64,128,256)) {
 $bmp=Make-Bitmap $size
 $stream=New-Object IO.MemoryStream
 $writer=New-Object IO.BinaryWriter($stream)
 $maskStride=[int]([Math]::Ceiling($size/32.0)*4)
 $writer.Write([int]40);$writer.Write([int]$size);$writer.Write([int]($size*2));$writer.Write([uint16]1);$writer.Write([uint16]32)
 $writer.Write([int]0);$writer.Write([int]($size*$size*4+$maskStride*$size));1..4|ForEach-Object{$writer.Write([int]0)}
 for($y=$size-1;$y -ge 0;$y--){for($x=0;$x -lt $size;$x++){$c=$bmp.GetPixel($x,$y);$writer.Write([byte]$c.B);$writer.Write([byte]$c.G);$writer.Write([byte]$c.R);$writer.Write([byte]$c.A)}}
 for($y=$size-1;$y -ge 0;$y--){$mask=New-Object byte[] $maskStride;for($x=0;$x -lt $size;$x++){if($bmp.GetPixel($x,$y).A -eq 0){$index=[int][Math]::Floor($x/8);$mask[$index]=$mask[$index] -bor (128 -shr ($x%8))}};$writer.Write($mask)}
 $writer.Flush();$frames+=@{Size=$size;Bytes=$stream.ToArray()};$writer.Dispose();$bmp.Dispose()
}
$file=[IO.File]::Create((Join-Path $assets 'whale.ico'));$ico=New-Object IO.BinaryWriter($file)
$ico.Write([uint16]0);$ico.Write([uint16]1);$ico.Write([uint16]$frames.Count)
$offset=6+16*$frames.Count
foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$ico.Write([byte]$dimension);$ico.Write([byte]$dimension);$ico.Write([uint16]0);$ico.Write([uint16]1);$ico.Write([uint16]32);$ico.Write([int]$frame.Bytes.Length);$ico.Write([int]$offset);$offset+=$frame.Bytes.Length}
foreach($frame in $frames){$ico.Write([byte[]]$frame.Bytes)}
$ico.Dispose()

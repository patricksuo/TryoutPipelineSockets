Remove-Item -LiteralPath echo_release/  -Recurse  -ErrorAction SilentlyContinue
Remove-Item -LiteralPath echo_release.tar -ErrorAction SilentlyContinue

New-Item -Name echo_release -ItemType "directory"
dotnet.exe publish -o ..\echo_release\Server\ .\EchoServer\
dotnet.exe publish -o ..\echo_release\Client\ .\EchoClient\
tar.exe cf echo_release.tar echo_release
Remove-Item -LiteralPath echo_release/ -Recurse -ErrorAction SilentlyContinue

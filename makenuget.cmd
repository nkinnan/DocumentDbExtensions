@echo off
echo =======================================================
echo Did you remember to:
echo 1) build in release mode
echo 2) update the nuspec to the new revision # and release notes
echo 3) update the dll properties to the new revision #
echo =======================================================
pause
nuget pack Package.nuspec
pause

@echo off
@chcp 65001>nul

rem Složka na sdíleném disku
set qpath=Q:\Builds\dxf2fcs\


rem Složení jména z datumu ve formátu dxf2fcs_YYMMDD
for /f "skip=1" %%d in ('wmic os get localdatetime') do if not defined mydate set mydate=%%d
set name=dxf2fcs_%mydate:~2,2%%mydate:~4,2%%mydate:~6,2%
rem echo %name%


rem Nalezení volné přípony, pokud proběhlo v jeden více releasů
FOR %%G IN (a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t,u,v,w,x,y,z) DO (
    if exist %qpath%%name%%%G.zip (
        rem file exists
    ) else (
        rem file doesn't exist
        set name=%name%%%G
        goto forexit
    )
)

:forexit
rem echo %name%


rem Odstranění publish složky
if exist bin\publish (
    rd /s /q bin\publish
)


rem Sestavení a zabalení
dotnet publish -o bin/publish -c Release -r win-x86 --self-contained false


cd bin


rem Zabalit do zip (bandizip)
bc c %name%.zip publish


rem Nahrát na sdílený disk
copy %name%.zip %qpath%%name%.zip

cd ..

echo Done.

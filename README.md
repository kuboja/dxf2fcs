# dxf2fcs

Converter for dxf to [FemCAD script](https://github.com/HiStructClient/femcad-doc/wiki) geometry. It's command line tool, that can create fcs with linear geometry from dxf file. The resulting file can be loaded on [HiStruct](https://www.histruct.com) or opened in FemCAD.

You can run the tool from command line with this command: (You must be in the folder with .exe file)

```
dxf2fcs c:\dxf\plan.dxf
```

Fcs file will be created and saved to the same folder.

## Command line parameters

### Precision

Default precision is 5 decimal places (0.1 mm). For change you can use `-p number` command line parameter:

```
dxf2fcs c:\dxf\plan.dxf -p 6
```

### Dxf unit
Unit is used for size transformation from dxf unit to femcad unit (m). Supported units are:
- mm (0.001, default)
- m (1.0).

Use `-u [mm|m]` parameter for change unit:

```
dxf2fcs c:\dxf\plan.dxf -u m
```

### Output file
You can define the output file path with `-o fullpath` parameter:
```
dxf2fcs c:\dxf\plan.dxf -o c:\fcs\plan.fcs
```

1、实际编译时候需要注意【build_release.bat】里面开始时候配置的各种路径，根据实际情况修改
2、电脑需要安装【Inno Setup Compiler】，然后根据实际情况修改【TaskDispatcher.iss】里面的路径
3、编译时，通过管理员权限运行CMD，然后运行【.\build_release.bat】，最后会在用户指定的目录生成一个【TaskDispatcher_Setup.exe】，注意：这是个安装包
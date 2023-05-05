<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Logout.aspx.cs" Inherits="Company_Logout" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
        <div>

            <ext:ResourceManager ID="ResourceManager1" runat="server" StateProvider="LocalStorage" />
            <ext:Window
                ID="LoginWindow"
                meta:resourcekey="LoginWindow"
                runat="server"
                Title="登出系統"
                Icon="Application"
                Height="185"
                Width="300"
                BodyStyle="background-color: #fff;"
                BodyPadding="5"
                Modal="false">
                <Items>
                    <ext:Label ID="Label1" runat="server" meta:resourcekey="Label1" Text="請確認是否要登出系統?"></ext:Label>
                </Items>
                <DockedItems>
                    <ext:Toolbar Dock="Bottom" runat="server">
                        <Items>
                            <ext:Component runat="server" Flex="1"></ext:Component>
                            <ext:Button Icon="Accept" meta:resourcekey="Accept" runat="server" ID="btnLogout" Text="確認">
                                <DirectEvents>
                                    <Click OnEvent="btnLogout_Click">
                                        <EventMask ShowMask="true" Msg="登出中..." MinDelay="100" />
                                    </Click>
                                </DirectEvents>
                            </ext:Button>
                            <ext:Button ID="btnClose" meta:resourcekey="btnClose" runat="server" Text="關閉">
                               
                            </ext:Button>
                        </Items>
                    </ext:Toolbar>
                </DockedItems>
            </ext:Window>
        </div>
    </form>
</body>
</html>

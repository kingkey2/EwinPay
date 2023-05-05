<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Login.aspx.cs" Inherits="Login" %>

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
                Title="SkyPay 後台管理"
                Icon="Application"
                Height="250"
                Width="400"
                BodyStyle="background-color: #fff;"
                BodyPadding="5"
                Modal="false">
                <Items>
                    <ext:TextField ID="txtAccount" meta:resourcekey="txtAccount" runat="server" FieldLabel="登入帳號" AutoFocus="True">
                    </ext:TextField>
                    <ext:TextField ID="txtPassword" meta:resourcekey="txtPassword" runat="server" InputType="Password" FieldLabel="登入密碼">
                    </ext:TextField>
                    <ext:TextField ID="txtCompanyCode" meta:resourcekey="txtCompanyCode" runat="server" FieldLabel="商戶號">
                    </ext:TextField>
                </Items>
                <DockedItems>
                    <ext:Toolbar Dock="Bottom" runat="server">
                        <Items>
                            <ext:Button runat="server" ID="btnLangC" EnableToggle="true" ToggleGroup="Group1"  Text="繁中" />
                            <ext:Component runat="server" Flex="1"></ext:Component>
                            <ext:Button Icon="Accept" runat="server" ID="btnLogin" meta:resourcekey="btnLogin" Text="登入">
                                <DirectEvents>
                                    <Click OnEvent="btnLogin_Click">
                                        <EventMask ShowMask="true" Msg="檢查中..." MinDelay="500" />
                                    </Click>
                                </DirectEvents>
                            </ext:Button>
                        </Items>
                    </ext:Toolbar>
                </DockedItems>
            </ext:Window>
        </div>
    </form>
</body>
</html>

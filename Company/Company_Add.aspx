<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Company_Add.aspx.cs" Inherits="Company_Company_Add" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title></title>
</head>
<script>
</script>
<body>
    <form id="form1" runat="server">
        <div>
            <ext:ResourceManager ID="ResourceManager1" runat="server" StateProvider="LocalStorage" />
            <ext:FormPanel
                ID="Panel1"
                meta:resourcekey="Panel1"
                runat="server"
                Title="公司帳號新增"
                BodyPaddingSummary="5 5 0"
                Width="650"
                Frame="true"
                Resizable="true"
                ButtonAlign="Center"
                Layout="FormLayout">
                <FieldDefaults MsgTarget="Side" LabelWidth="75" />
                <Plugins>
                    <ext:DataTip ID="DataTip1" runat="server" />
                </Plugins>
                <Items>
                    <ext:FieldSet ID="FieldSet1" meta:resourcekey="FieldSet1" runat="server"
                        Flex="1"
                        Title="基本資料"
                        Layout="AnchorLayout">
                        <Items>
                            <ext:TextField ID="txtCompanyCode" meta:resourcekey="txtCompanyCode" runat="server" FieldLabel="公司代碼" AllowBlank="false" AutoFocus="true" IsRemoteValidation="true">
                                <RemoteValidation OnValidation="CheckCompanyCodeExist" />
                            </ext:TextField>
                            <ext:TextField ID="txtCompanyName" meta:resourcekey="txtCompanyName" runat="server" FieldLabel="公司名稱" AllowBlank="false"></ext:TextField>
                            <ext:TextField ID="txtCompanyUserRate" meta:resourcekey="txtCompanyUserRate" runat="server" FieldLabel="公司佔成率" AllowBlank="false"></ext:TextField>
                            <ext:TextField ID="txtCompanyBuyChipRate" meta:resourcekey="txtCompanyBuyChipRate" runat="server" FieldLabel="公司轉碼率" AllowBlank="false"></ext:TextField>
                            <ext:CheckboxGroup 
                                ID="groupCompanyChipsList" 
                                meta:resourcekey="groupCompanyChipsList"
                                FieldLabel="允許顯示籌碼"
                                runat="server" 
                                Width="500"
                                ColumnsNumber="6">
                               <Items>
                                    <ext:Checkbox runat="server" ID="Chip1" BoxLabel="1" InputValue="1"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip5" BoxLabel="5" InputValue="5"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip10" BoxLabel="10" InputValue="10"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip25" BoxLabel="25" InputValue="25"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip50" BoxLabel="50" InputValue="50"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip100" BoxLabel="100" InputValue="100"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip250" BoxLabel="250" InputValue="250"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip500" BoxLabel="500" InputValue="500"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip1000" BoxLabel="1,000" InputValue="1000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip1250" BoxLabel="1,250" InputValue="1250"></ext:Checkbox>
                                    <ext:Checkbox runat="server" ID="Chip5000" BoxLabel="5,000" InputValue="5000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" meta:resourcekey="Chip10000" ID="Chip10000" BoxLabel="1萬" InputValue="10000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" meta:resourcekey="Chip50000" ID="Chip50000" BoxLabel="5萬" InputValue="50000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" meta:resourcekey="Chip100000" ID="Chip100000" BoxLabel="10萬" InputValue="100000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" meta:resourcekey="Chip500000" ID="Chip500000" BoxLabel="50萬" InputValue="500000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" meta:resourcekey="Chip1000000" ID="Chip1000000" BoxLabel="100萬" InputValue="1000000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" meta:resourcekey="Chip5000000" ID="Chip5000000" BoxLabel="500萬" InputValue="5000000"></ext:Checkbox>
                                    <ext:Checkbox runat="server" meta:resourcekey="Chip10000000" ID="Chip10000000" BoxLabel="1,000萬" InputValue="10000000"></ext:Checkbox>
                                </Items>
                            </ext:CheckboxGroup>
                            <ext:CheckboxGroup 
                                ID="groupCompanyCurrentTypeList" 
                                meta:resourcekey="groupCompanyCurrentTypeList"
                                FieldLabel="允許使用貨幣"
                                runat="server" 
                                Width="500"
                                ColumnsNumber="6">
                               <Items>                                    
                                </Items>
                            </ext:CheckboxGroup>
                            <ext:RadioGroup 
                                ID="groupDefaultCurrencyType" 
                                meta:resourcekey="groupDefaultCurrencyType"
                                GroupName="groupDefaultCurrencyType"
                                FieldLabel="預設貨幣"
                                runat="server" 
                                Width="500"
                                ColumnsNumber="6"
                                >
                               <Items>                                    
                                </Items>
                            </ext:RadioGroup>
                            <ext:TextField ID="txtDescription" meta:resourcekey="txtDescription" runat="server" FieldLabel="公司描述" AllowBlank="false"></ext:TextField>
                            <ext:TextField ID="txtDomainURL" meta:resourcekey="txtDomainURL" runat="server" FieldLabel="對外網址" AllowBlank="false"></ext:TextField>
                            <ext:TextField ID="txtGuestAccount" meta:resourcekey="txtGuestAccount" runat="server" FieldLabel="訪客帳號" AllowBlank="false" Width="400" EmptyText="(不開放請保留空白)" ></ext:TextField>
                            <ext:TextField ID="txtCompanyEthWalletAddress" meta:resourcekey="txtCompanyEthWalletAddress" runat="server" FieldLabel="乙太坊總錢包地址" AllowBlank="false" Width="400"></ext:TextField>

                        </Items>
                    </ext:FieldSet>
                    <ext:FieldSet ID="FieldSet2" meta:resourcekey="FieldSet2" runat="server"
                        Flex="1"
                        Title="新增管理者帳號"
                        Layout="AnchorLayout">
                        <Items>
                            <ext:TextField ID="txtLoginAccount" meta:resourcekey="Panel1" runat="server"  FieldLabel="登入帳號" AllowBlank="false" IsRemoteValidation="true">
                            </ext:TextField>
                            <ext:TextField
                                ID="txtLoginPassword"
                                meta:resourcekey="txtLoginPassword"
                                runat="server"
                                FieldLabel="登入密碼"
                                AllowBlank="false"
                                InputType="Password">
                                <Listeners>
                                    <ValidityChange Handler="this.next().validate();" />
                                    <Blur Handler="this.next().validate();" />
                                </Listeners>
                            </ext:TextField>
                            <ext:TextField
                                ID="txtLoginPassword2"
                                meta:resourcekey="txtLoginPassword2"
                                runat="server"
                                MsgTarget="Side"
                                Vtype="password"
                                FieldLabel="確認密碼"
                                AllowBlank="false"
                                InputType="Password">
                                <CustomConfig>
                                    <ext:ConfigItem Name="initialPassField" Value="txtLoginPassword" Mode="Value" />
                                </CustomConfig>
                            </ext:TextField>
                            <ext:TextField ID="txtRealname" meta:resourcekey="txtRealname" runat="server" FieldLabel="姓名" AllowBlank="true"></ext:TextField>
                            <ext:TextField ID="TextField1" meta:resourcekey="TextField1" runat="server" FieldLabel="描述" AllowBlank="true"></ext:TextField>
                        </Items>
                    </ext:FieldSet>
                </Items>
                <Buttons>
                    <ext:Button ID="btnSave" meta:resourcekey="btnSave" runat="server" Text="送出" Icon="Disk">
                        <DirectEvents>
                            <Click OnEvent="btnSave_DirectClick">
                                <EventMask ShowMask="true" Msg="Wait..." MinDelay="500" />
                            </Click>
                        </DirectEvents>
                    </ext:Button>
                    <ext:Button ID="btnClose" meta:resourcekey="btnClose" runat="server" Text="關閉">
                        <DirectEvents>
                            <Click OnEvent="btnClose_DirectClick">
                                <ExtraParams>
                                    <ext:Parameter Name="GetEnter" Value="e.getKey()" Mode="Raw">
                                    </ext:Parameter>
                                </ExtraParams>
                            </Click>
                        </DirectEvents>
                    </ext:Button>
                </Buttons>
            </ext:FormPanel>

        </div>
    </form>
</body>
</html>

<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Company_Edit.aspx.cs" Inherits="Company_Company_Edit" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title></title>
</head>
    <script>
            var Selector = {
            add: function () {
                var oGridCompanyRoadMap;

                oGridCompanyRoadMap = Ext.getCmp("gridCompanyRoadMap");

                processRule(oGridCompanyRoadMap, "CompanyRoadMap");
            }
        };

        function processRule(sender, inputName) {
            var oForm;
            var cNodes;

            oForm = document.getElementById("form1");
            cNodes = document.getElementsByName(inputName);
            if (cNodes) {
                if (cNodes.length > 0) {
                    for (i = 0; i < cNodes.length; i++) {
                        oForm.removeChild(cNodes[i]);
                    }
                }
            }

            if (sender.selModel.hasSelection()) {
                var sItems = sender.selModel.getSelection();

                if (sItems) {
                    if (sItems.length > 0) {
                        for (i = 0; i < sItems.length; i++) {
                            var roadMapID = sItems[i].get("RoadMapID");
                            var oINPUT;

                            oINPUT = document.createElement("INPUT");
                            oINPUT.name = inputName;
                            oINPUT.type = "hidden";
                            oINPUT.value = roadMapID;

                            oForm.appendChild(oINPUT);
                        }
                    }
                }
            }
        }
    </script>
<body>
    <form id="form1" runat="server">
        <div>
            <ext:ResourceManager ID="ResourceManager1" runat="server" StateProvider="LocalStorage" />
            <ext:FormPanel
                ID="Panel1"
                meta:resourcekey="Panel1"
                runat="server"
                Title="公司帳號編輯"
                BodyPaddingSummary="5 5 0"
                Width="650"
                Frame="true"
                Resizable ="true"
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
                        Layout="AnchorLayout"
                        >
                        <Items>
                            <ext:FieldContainer 
                                ID="FieldContainerResource1" 
                                meta:resourcekey="FieldContainerResource1"
                                runat="server" 
                                FieldLabel="公司代碼" 
                                AnchorHorizontal="100%" 
                                Layout="HBoxLayout">                                       
                                <Items>
                                    <ext:Label ID="lblCompanyCode" runat="server" Text=""></ext:Label>
                                </Items>
                            </ext:FieldContainer>
                            <ext:TextField ID="txtCompanyName" meta:resourcekey="txtCompanyName" runat="server" FieldLabel="公司名稱"  AutoFocus="true" AllowBlank="false"></ext:TextField>
                            <ext:TextField ID="txtDescription" meta:resourcekey="txtDescription" runat="server" FieldLabel="公司描述" AllowBlank="false"></ext:TextField>
                            <ext:RadioGroup ID="RadioGroup3" meta:resourcekey="RadioGroup3" runat="server" FieldLabel="帳戶狀態" ColumnsNumber="3" AutomaticGrouping="false">
                                <Items>
                                    <ext:Radio ID="rdoCompanyStateNormal" meta:resourcekey="rdoCompanyStateNormal" runat="server" Name="rdoAdminState" InputValue="0" BoxLabel="正常" />
                                    <ext:Radio ID="rdoCompanyStateDisable" meta:resourcekey="rdoCompanyStateDisable" runat="server" Name="rdoAdminState" InputValue="1" BoxLabel="停用" />
                                </Items>
                            </ext:RadioGroup>
                                <ext:TextField ID="txtCompanyUserRate" meta:resourcekey="txtCompanyUserRate" runat="server" FieldLabel="公司佔成率(%)" AllowBlank="false"></ext:TextField>
                               <ext:TextField ID="txtCompanyBuyChipRate" meta:resourcekey="txtCompanyBuyChipRate" runat="server" FieldLabel="公司轉碼率(%)" AllowBlank="false"></ext:TextField>
                               <ext:CheckboxGroup 
                                ID="groupCompanyChipsList" 
                                FieldLabel="允許顯示籌碼"
                                   meta:resourcekey="groupCompanyChipsList"
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
                             <ext:FieldSet ID="FieldSet5" meta:resourcekey="FieldSet5" runat="server"
                                 Flex="1"
                                 Title="可管理路單"
                                 Layout="AnchorLayout"
                                 width="300"
                                 >
                                 <Items>
                                     <ext:GridPanel
                                         ID="gridCompanyRoadMap"
                                         runat="server"
                                         Height="300"
                                         Flex="1">
                                         <Store>
                                             <ext:Store runat="server" PageSize="10">
                                                 <Model>
                                                     <ext:Model runat="server" IDProperty="RoadMapID">
                                                         <Fields>
                                                             <ext:ModelField Name="RoadMapID" />
                                                             <ext:ModelField Name="RoadMapNumber" />
                                                             <ext:ModelField Name="AreaName" />
                                                         </Fields>
                                                     </ext:Model>
                                                 </Model>
                                             </ext:Store>
                                         </Store>
                                         <ColumnModel runat="server">
                                             <Columns>
                                                 <ext:Column ID="Column1" meta:resourcekey="Column1" runat="server" Text="路單編號" Width="100" DataIndex="RoadMapNumber" />
                                                 <ext:Column ID="Column2" meta:resourcekey="Column2" runat="server" Text="地點" Width="100" DataIndex="AreaName" />
                                             </Columns>
                                         </ColumnModel>
                                         <BottomBar>
                                         </BottomBar>
                                         <SelectionModel>
                                             <ext:CheckboxSelectionModel runat="server" Mode="Multi" />
                                         </SelectionModel>
                                     </ext:GridPanel>
                                 </Items>
                             </ext:FieldSet> 

                             <ext:FieldContainer
                                 ID="FieldContainerResource2"
                                 Meta:resourcekey="FieldContainerResource2"
                                 runat="server"
                                 FieldLabel="跑馬燈資訊"
                                 AnchorHorizontal="100%"
                                 Layout="HBoxLayout">
                                 <Items>
                                     <ext:Button ID="btnAddUserStockValue" meta:resourcekey="btnAddUserStockValue" runat="server" Text="新增資訊">
                                          <DirectEvents>
                                                <Click OnEvent="btnAddMarqueeText_DirectClick"></Click>
                                          </DirectEvents>
                                     </ext:Button>
                                 </Items>
                             </ext:FieldContainer>
                            <ext:TextField ID="txtDomainURL" meta:resourcekey="txtDomainURL" runat="server" FieldLabel="對外網址" AllowBlank="false"></ext:TextField>
                            <ext:TextField ID="txtGuestAccount" meta:resourcekey="txtGuestAccount" runat="server" FieldLabel="訪客帳號" AllowBlank="false" Width="400" EmptyText="(不開放請保留空白)" ></ext:TextField>
                            <ext:TextField ID="txtCompanyEthWalletAddress" meta:resourcekey="txtCompanyEthWalletAddress" runat="server" FieldLabel="乙太坊總錢包地址" AllowBlank="false" Width="400"></ext:TextField>

                        </Items>
                    </ext:FieldSet>
                     <ext:FieldSet ID="FieldSet2" meta:resourcekey="FieldSet2" runat="server"
                        Flex="1"
                        Title="管理者帳號"
                        Layout="AnchorLayout"
                        >
                        <Items>
                            <ext:TextField ID="txtLoginAccount" meta:resourcekey="txtLoginAccount" runat="server" FieldLabel="登入帳號" AllowBlank="false" IsRemoteValidation="true">
                            </ext:TextField>
                            <ext:TextField 
                                ID="txtLoginPassword" 
                                meta:resourcekey="txtLoginPassword"
                                runat="server" 
                                FieldLabel="登入密碼" 
                                AllowBlank="false" 
                                InputType="Password" >
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
                        <Listeners>
                            <Click Handler="Selector.add();" />
                        </Listeners>
                        <DirectEvents>
                            <Click OnEvent="btnSave_DirectClick">
                                <EventMask ShowMask="true" Msg="Wait..." MinDelay="500" />
                            </Click>
                        </DirectEvents>
                    </ext:Button>
                    <ext:Button ID="btnClose" meta:resourcekey="btnClose" runat="server" Text="關閉" >
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

(self.webpackChunk_N_E=self.webpackChunk_N_E||[]).push([[4496],{5477:function(e,t,o){"use strict";var n=o(7462),i=o(5987),r=o(7294),a=(o(5697),o(6010)),s=o(1591),c=o(3871),l=44;function d(e){var t,o,n;return t=e,o=0,n=1,e=(Math.min(Math.max(o,t),n)-o)/(n-o),e=(e-=1)*e*e+1}var u=r.forwardRef((function(e,t){var o,s=e.classes,u=e.className,h=e.color,m=void 0===h?"primary":h,x=e.disableShrink,p=void 0!==x&&x,f=e.size,g=void 0===f?40:f,v=e.style,w=e.thickness,b=void 0===w?3.6:w,j=e.value,y=void 0===j?0:j,k=e.variant,N=void 0===k?"indeterminate":k,Z=(0,i.Z)(e,["classes","className","color","disableShrink","size","style","thickness","value","variant"]),C={},S={},D={};if("determinate"===N||"static"===N){var R=2*Math.PI*((l-b)/2);C.strokeDasharray=R.toFixed(3),D["aria-valuenow"]=Math.round(y),"static"===N?(C.strokeDashoffset="".concat(((100-y)/100*R).toFixed(3),"px"),S.transform="rotate(-90deg)"):(C.strokeDashoffset="".concat((o=(100-y)/100,o*o*R).toFixed(3),"px"),S.transform="rotate(".concat((270*d(y/70)).toFixed(3),"deg)"))}return r.createElement("div",(0,n.Z)({className:(0,a.Z)(s.root,u,"inherit"!==m&&s["color".concat((0,c.Z)(m))],{indeterminate:s.indeterminate,static:s.static}[N]),style:(0,n.Z)({width:g,height:g},S,v),ref:t,role:"progressbar"},D,Z),r.createElement("svg",{className:s.svg,viewBox:"".concat(22," ").concat(22," ").concat(l," ").concat(l)},r.createElement("circle",{className:(0,a.Z)(s.circle,p&&s.circleDisableShrink,{indeterminate:s.circleIndeterminate,static:s.circleStatic}[N]),style:C,cx:l,cy:l,r:(l-b)/2,fill:"none",strokeWidth:b})))}));t.Z=(0,s.Z)((function(e){return{root:{display:"inline-block"},static:{transition:e.transitions.create("transform")},indeterminate:{animation:"$circular-rotate 1.4s linear infinite"},colorPrimary:{color:e.palette.primary.main},colorSecondary:{color:e.palette.secondary.main},svg:{display:"block"},circle:{stroke:"currentColor"},circleStatic:{transition:e.transitions.create("stroke-dashoffset")},circleIndeterminate:{animation:"$circular-dash 1.4s ease-in-out infinite",strokeDasharray:"80px, 200px",strokeDashoffset:"0px"},"@keyframes circular-rotate":{"0%":{transformOrigin:"50% 50%"},"100%":{transform:"rotate(360deg)"}},"@keyframes circular-dash":{"0%":{strokeDasharray:"1px, 200px",strokeDashoffset:"0px"},"50%":{strokeDasharray:"100px, 200px",strokeDashoffset:"-15px"},"100%":{strokeDasharray:"100px, 200px",strokeDashoffset:"-125px"}},circleDisableShrink:{animation:"none"}}}),{name:"MuiCircularProgress",flip:!1})(u)},7812:function(e,t,o){"use strict";var n=o(7462),i=o(5987),r=o(7294),a=(o(5697),o(6010)),s=o(1591),c=o(9693),l=o(1810),d=o(3871),u=r.forwardRef((function(e,t){var o=e.edge,s=void 0!==o&&o,c=e.children,u=e.classes,h=e.className,m=e.color,x=void 0===m?"default":m,p=e.disabled,f=void 0!==p&&p,g=e.disableFocusRipple,v=void 0!==g&&g,w=e.size,b=void 0===w?"medium":w,j=(0,i.Z)(e,["edge","children","classes","className","color","disabled","disableFocusRipple","size"]);return r.createElement(l.Z,(0,n.Z)({className:(0,a.Z)(u.root,h,"default"!==x&&u["color".concat((0,d.Z)(x))],f&&u.disabled,"small"===b&&u["size".concat((0,d.Z)(b))],{start:u.edgeStart,end:u.edgeEnd}[s]),centerRipple:!0,focusRipple:!v,disabled:f,ref:t},j),r.createElement("span",{className:u.label},c))}));t.Z=(0,s.Z)((function(e){return{root:{textAlign:"center",flex:"0 0 auto",fontSize:e.typography.pxToRem(24),padding:12,borderRadius:"50%",overflow:"visible",color:e.palette.action.active,transition:e.transitions.create("background-color",{duration:e.transitions.duration.shortest}),"&:hover":{backgroundColor:(0,c.U1)(e.palette.action.active,e.palette.action.hoverOpacity),"@media (hover: none)":{backgroundColor:"transparent"}},"&$disabled":{backgroundColor:"transparent",color:e.palette.action.disabled}},edgeStart:{marginLeft:-12,"$sizeSmall&":{marginLeft:-3}},edgeEnd:{marginRight:-12,"$sizeSmall&":{marginRight:-3}},colorInherit:{color:"inherit"},colorPrimary:{color:e.palette.primary.main,"&:hover":{backgroundColor:(0,c.U1)(e.palette.primary.main,e.palette.action.hoverOpacity),"@media (hover: none)":{backgroundColor:"transparent"}}},colorSecondary:{color:e.palette.secondary.main,"&:hover":{backgroundColor:(0,c.U1)(e.palette.secondary.main,e.palette.action.hoverOpacity),"@media (hover: none)":{backgroundColor:"transparent"}}},disabled:{},sizeSmall:{padding:3,fontSize:e.typography.pxToRem(18)},label:{width:"100%",display:"flex",alignItems:"inherit",justifyContent:"inherit"}}}),{name:"MuiIconButton"})(u)},6535:function(e,t,o){(window.__NEXT_P=window.__NEXT_P||[]).push(["/deposit/c2c/jp/default/[orderId]",function(){return o(1832)}])},8351:function(e,t,o){"use strict";o.d(t,{Z:function(){return l}});var n=o(5893),i=o(7294),r=o(1163),a=(o(9188),o(282)),s=o(7812),c=o(9943);function l(e){var t=(0,r.useRouter)(),o=(0,c.R)(t.query.lang),l=e.className,d=e.data,u=e.display,h=void 0===u?"text":u,m=(0,i.useState)(o.copy),x=m[0],p=m[1],f=(0,i.useRef)(null),g=null,v="text"===h?a.Z:s.Z;switch(h){case"text":g=null;break;case"button":g="/img/icon/copy.svg";break;case"button-black":g="/img/icon/copy-black.svg"}return(0,n.jsxs)(n.Fragment,{children:[(0,n.jsx)("textarea",{readOnly:!0,style:{width:0,height:0,position:"absolute",opacity:0},value:d,ref:f}),(0,n.jsx)(v,{className:l,onClick:function(){var e;null===(e=f.current)||void 0===e||e.select(),document.execCommand("copy"),p(o.success)},children:"text"===h?x:(0,n.jsx)("img",{className:l,src:g})})]})}},9023:function(e,t,o){"use strict";o.d(t,{Z:function(){return C}});var n=o(9534),i=o(5893),r=o(3832),a=o(7294),s=o(7703),c=o(1120),l=o(1749),d=o(5518),u=o(1163),h=(o(9188),o(8805)),m=o(9943),x=o(9923),p=o(6486),f=o.n(p),g=o(5955),v=o(4516),w=o(8351),b=o(8267),j=function(e){return-1!==["default","v2","dpp","kmt"].indexOf(e)},y=o(2555),k=o(6534),N=function(e){var t=v.x[e],o=t.mainColor1,n=t.mainColorImage1,i=t.subColor3,r=t.infoTextColor,a=t.infoTitleColor;return(0,c.Z)({containerWithMask:{maxWidth:800,borderRadius:10,boxShadow:"0 0 20px 0 #1b2444",backgroundColor:"white",padding:0},containerWithMaskMobile:{maxWidth:800,backgroundColor:"white",padding:0},orderInfo:{width:"100%",backgroundColor:o,backgroundImage:n,margin:"0px",borderTopLeftRadius:"inherit",borderTopRightRadius:"inherit"},infoBox:{display:"flex",justifyContent:"space-between",alignItems:"center",height:67,margin:"0px 10px 10px 10px",borderRadius:10,backgroundColor:i},infoBoxUtr:{display:"flex",justifyContent:"space-between",alignItems:"center",height:38,margin:"0px 10px 10px 10px",borderRadius:10,backgroundColor:i},infoBoxInner:{height:"100%",display:"flex",flexDirection:"column",justifyContent:"center",marginLeft:10},infoTitle:{color:a,fontSize:14},infoTitleUtr:{width:130,color:a,fontSize:16,fontWeight:500,marginLeft:10},infoValue1:{color:r,fontSize:24,lineHeight:"24px",fontWeight:"bold",marginTop:6},infoValueUtr:{width:"calc(100% - 160px)",color:r,fontSize:16,fontWeight:500,lineHeight:"21px",textAlign:"right"},infoValue2:{color:r,fontSize:18,lineHeight:"18px",fontWeight:"bold",marginTop:5},upiBox:{width:"100%",maxWidth:360,margin:"0px auto auto auto",display:"flex",flexDirection:"column",alignItems:"center"},secureBy:{margin:"20px 0px 24px 0px",height:26,lineHeight:"26px",display:"flex",justifyContent:"center",alignItems:"center",fontSize:12,fontWeight:500,color:"#545454"},copyButton:{width:30,height:30}})()};function Z(e){var t=e.theme,o=e.method,n=e.show,r=e.title,a=e.text;if(!n)return(0,i.jsx)(i.Fragment,{});var s=N(t);return"utr"===o?(0,i.jsxs)(l.Z,{item:!0,xs:12,className:s.infoBoxUtr,children:[(0,i.jsx)("div",{className:s.infoTitleUtr,children:r}),(0,i.jsx)("div",{className:s.infoValueUtr,children:a}),(0,i.jsx)(w.Z,{className:s.copyButton,data:a,display:"button"})]}):(0,i.jsxs)(l.Z,{item:!0,xs:12,className:s.infoBox,children:[(0,i.jsxs)("div",{className:s.infoBoxInner,children:[(0,i.jsx)("div",{className:s.infoTitle,children:r}),(0,i.jsx)("div",{className:s.infoValue2,children:a})]}),(0,i.jsx)(w.Z,{data:a,display:"button"})]})}function C(e){var t=(0,u.useRouter)().query.lang,o=e.orderDeposit,c=e.method,p=e.theme,v=e.countdown,w=void 0===v?"--:--":v,C=e.region,S=null!==o&&void 0!==o?o:{},D=S.state,R=S.merchantOrderNo,O=void 0===R?"-":R,B=S.amountRequested,I=void 0===B?0:B,T=S.depositMemo,z=void 0===T?"-":T,E=S.accountNo,W=void 0===E?"-":E,_=S.accountName,M=void 0===_?"-":_,P=D===h.OrderDepositState.Error||D===h.OrderDepositState.Canceled||D===h.OrderDepositState.Completed||D===h.OrderDepositState.Timeout,F={langBar:{show:!1},amount:{show:!0},orderNumber:{show:!0},accountNo:{show:!0},accountName:{show:!0},note:{show:!0},billDate:{show:!1},hint:{show:!0}};switch(null===o||void 0===o?void 0:o.clientState){case h.OrderDepositClientState.ShowQrcode:case h.OrderDepositClientState.InputUtr:case h.OrderDepositClientState.RequestPaymentFinished:F.accountNo.show=!0,"gcash"===p?(F.accountName.show=!0,F.note.show=!1):(F.accountName.show=!1,F.note.show=!0);break;case h.OrderDepositClientState.Pending:F.langBar.show="gcash"!==p,F.accountNo.show="utr"===c,F.accountName.show=!1,F.note.show="utr"===c;break;default:F.accountNo.show=!1,F.accountName.show=!1,F.note.show=!1}(P||"c2c"===c)&&(F.accountNo.show=!1,F.note.show=!1),"jpn"===C&&(F.langBar.show=!1,F.orderNumber.show=!1),"utr"===c&&(F.langBar.show=!1,F.accountNo.show=!1),(null===o||void 0===o?void 0:o.clientState)===h.OrderDepositClientState.PaymentVerifying&&(F.hint.show=!1),(null===o||void 0===o?void 0:o.state)===h.OrderDepositState.Completed&&(F.billDate.show=!0,F.hint.show=!1);var U=(0,a.useState)(!1),L=U[0],q=U[1],H=(0,m.R)(t),V=N(p);(0,a.useEffect)((function(){q(d.tq)}),[L]);var $,A=L?V.containerWithMaskMobile:V.containerWithMask,X=e.children,J=e.hint,Q=(0,n.Z)(e,["children","hint"]),G=a.cloneElement(null!==X&&void 0!==X?X:(0,i.jsx)(i.Fragment,{}),Q),K=a.cloneElement(null!==J&&void 0!==J?J:(0,i.jsx)(i.Fragment,{}),Q);return(0,i.jsxs)(s.Z,{children:[(0,i.jsxs)(r.Z,{className:A,children:[F.langBar.show&&(0,i.jsx)(g.Z,{theme:p,lang:t,clock:{show:!1},handleLanguageChange:function(e){var t=null===window||void 0===window?void 0:window.location,o=new URLSearchParams(t.search);o.set("lang",e.target.value),t.search=o.toString()}}),(0,i.jsxs)(l.Z,{container:!0,justify:"space-between",alignItems:"center",style:F.langBar.show?{borderRadius:"0"}:{},className:V.orderInfo,children:[(0,i.jsx)(l.Z,{item:!0,xs:12,children:(0,i.jsx)(y.Z,{orderDeposit:o,theme:p,amount:I,countdown:w})}),(0,i.jsx)(Z,{show:F.orderNumber.show,theme:p,method:c,title:f().startCase(H.orderNo)+":",text:O}),(0,i.jsx)(Z,{show:F.accountNo.show,theme:p,method:c,title:(j(p)?f().startCase("UPI ID"):H.accountNo)+":",text:W}),(0,i.jsx)(Z,{show:F.accountName.show,theme:p,method:c,title:f().startCase(H.accountName)+":",text:M}),(0,i.jsx)(Z,{show:F.note.show,theme:p,method:c,title:f().startCase(H.note)+":",text:z}),(0,i.jsx)(Z,{show:F.billDate.show,theme:p,method:c,title:f().startCase(H.billDate)+":",text:(0,x.Jx)(null!==($=null===o||void 0===o?void 0:o.updatedAt)&&void 0!==$?$:"")})]}),(0,i.jsxs)("div",{className:V.upiBox,children:[P?(0,i.jsx)(b.Z,{orderDeposit:o,theme:p}):G,(0,i.jsxs)("div",{className:V.secureBy,children:[(0,i.jsx)("div",{children:H.paymentSecuredBy}),"gcash"===p?(0,i.jsx)(k.Z,{style:{marginLeft:4},src:"/img/gcash.png"}):(0,i.jsx)("b",{style:{marginLeft:3},children:null===o||void 0===o?void 0:o.siteDisplayName})]})]})]}),F.hint.show&&K]})}},1832:function(e,t,o){"use strict";o.r(t),o.d(t,{__N_SSP:function(){return g},default:function(){return v}});var n=o(5893),i=(o(7294),o(9943)),r=o(1163),a=o(1120),s=o(1749),c=o(5477),l=o(9750),d=o(9023),u=o(6486),h=o.n(u),m=o(8351),x=(0,a.Z)({rowBlock:{width:300,minHeight:40,margin:"auto",padding:"0px 6px 0px 10px",borderRadius:"6px",backgroundColor:"#e6e6e6",display:"flex",justifyContent:"space-between",alignItems:"center"},rowTitle:{width:300,margin:"10px auto 5px auto",fontSize:14,fontWeight:600},rowContent:{margin:"10px 0px 10px 0px",width:180},copyButton:{color:"white",fontSize:12,minWidth:80,height:28,backgroundColor:"#58bfff"}});function p(e){var t=x(),o=e.title,i=e.content;return(0,n.jsxs)(n.Fragment,{children:[(0,n.jsx)("div",{className:t.rowTitle,children:h().startCase(o)+":"}),(0,n.jsxs)("div",{className:t.rowBlock,children:[(0,n.jsx)("span",{className:t.rowContent,children:i}),(0,n.jsx)(m.Z,{className:t.copyButton,data:i})]})]})}function f(e){var t=(0,r.useRouter)(),o=(0,i.R)(t.query.lang),a=e.orderDeposit,l=null!==a&&void 0!==a?a:{},d=l.bankName,u=void 0===d?"":d,h=l.accountName,m=void 0===h?"":h,x=l.accountNo,f=void 0===x?"":x,g=l.depositMemo,v=void 0===g?"":g,w=l.amountRequested,b=void 0===w?"":w,j=l.branchName,y=void 0===j?"":j,k=l.branchCode,N=void 0===k?"":k;return a?(0,n.jsxs)("div",{style:{marginTop:50},children:[(0,n.jsx)(s.Z,{item:!0,xs:12,children:o.remarks.c2c.jp[0]}),(0,n.jsx)("br",{}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:o.remarks.c2c.jp[1]}),(0,n.jsx)("br",{}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:o.remarks.c2c.jp[2]}),(0,n.jsx)("br",{}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:(0,n.jsx)(p,{title:o.bankName,content:u})}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:(0,n.jsx)(p,{title:o.branchName,content:"".concat(y,"\uff08").concat(N,"\uff09")})}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:(0,n.jsx)(p,{title:o.accountName,content:m})}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:(0,n.jsx)(p,{title:o.accountNo,content:f})}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:(0,n.jsx)(p,{title:o.amount,content:b+""})}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:(0,n.jsx)(p,{title:o.note,content:v})}),(0,n.jsx)("br",{}),(0,n.jsx)(s.Z,{item:!0,xs:12,children:o.footerHint}),(0,n.jsx)("br",{})]}):(0,n.jsx)(s.Z,{container:!0,justify:"center",style:{marginTop:50},children:(0,n.jsx)(s.Z,{item:!0,children:(0,n.jsx)(c.Z,{})})})}var g=!0;function v(){return(0,n.jsx)(l.Z,{children:(0,n.jsx)(d.Z,{hint:(0,n.jsx)(n.Fragment,{}),theme:"dpp",method:"c2c",region:"jpn",children:(0,n.jsx)(f,{})})})}},9534:function(e,t,o){"use strict";function n(e,t){if(null==e)return{};var o,n,i=function(e,t){if(null==e)return{};var o,n,i={},r=Object.keys(e);for(n=0;n<r.length;n++)o=r[n],t.indexOf(o)>=0||(i[o]=e[o]);return i}(e,t);if(Object.getOwnPropertySymbols){var r=Object.getOwnPropertySymbols(e);for(n=0;n<r.length;n++)o=r[n],t.indexOf(o)>=0||Object.prototype.propertyIsEnumerable.call(e,o)&&(i[o]=e[o])}return i}o.d(t,{Z:function(){return n}})}},function(e){e.O(0,[2840,4098,2290,2948,9943,9221,9774,2888,179],(function(){return t=6535,e(e.s=t);var t}));var t=e.O();_N_E=t}]);
﻿<%@ Page Language="C#" AutoEventWireup="true" CodeFile="GASHSyncNotify.aspx.cs" Inherits="GASHSyncNotify" %>

<!doctype html>
<html>
<head>
    <meta charset="utf-8">

    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta http-equiv="X-UA-Compatible" content="IE=edge,chrome=1">
    <meta http-equiv="Content-Language" content="zh-tw" />
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta http-equiv="cache-control" content="no-cache" />
    <meta http-equiv="pragma" content="no-cache" />
    <meta http-equiv="expires" content="0" />
    <meta name="mobile-web-app-capable" content="yes" />
    <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
    <meta name="apple-mobile-web-app-capable" content="yes" />
    <meta name="format-detection" content="telephone=no" />
    <meta name="Description" content="">
    <meta name="viewport" content="width=device-width,initial-scale=1.0, minimal-ui, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <title>Info Page</title>
    <style type="text/css">
        html, body {
            width: 100%;
            height: 100%;
            padding: 0px;
            margin: 0px;
            font-family: '微軟正黑體', sans-serif, Helvetica Neue, Helvetica, Arial;
            background: #f1f1f1;
        }

        .mainInfo{
		background: -webkit-linear-gradient(135deg,#fff,#eee);
		background: -o-linear-gradient(135deg,#fff,#eee);
		background: -moz-linear-gradient(135deg,#fff,#eee);
		background: linear-gradient(135deg,#fff,#eee);
		-webkit-box-shadow: 0px 1px 10px 0px rgba(0,0,0,0.1);
		-moz-box-shadow: 0px 1px 10px 0px rgba(0,0,0,0.1);
		-o-box-shadow: 0px 1px 10px 0px rgba(0,0,0,0.1);
		box-shadow: 0px 1px 10px 0px rgba(0,0,0,0.1);
	    -webkit-animation: mainInfoAni 0.3s linear  forwards;
        -moz-animation: mainInfoAni 0.3s linear  forwards;
        -ms-animation: mainInfoAni 0.3s linear  forwards;
        -o-animation: mainInfoAni 0.3s linear  forwards;
        animation: mainInfoAni 0.3s linear  forwards;

		}
		@-webkit-keyframes mainInfoAni {
           from {
			 opacity: 0.5;
             margin-top: 40px;
           }
           to {
			 opacity: 1.0;
             margin-top: 0px;
           }
         }
		@-moz-keyframes mainInfoAni {
           from {
			 opacity: 0.5;
             margin-top: 40px;
           }
           to {
			 opacity: 1.0;
             margin-top: 0px;
           }
         }
		@-o-keyframes mainInfoAni {
           from {
			 opacity: 0.5;
             margin-top: 40px;
           }
           to {
			 opacity: 1.0;
             margin-top: 0px;
           }
         }
		@keyframes mainInfoAni {
           from {
			 opacity: 0.5;
             margin-top: 40px;
           }
           to {
			 opacity: 1.0;
             margin-top: 0px;
           }
         }
	.mainWrapper{
		width: 100%;
		height: 100%;
		display: flex;
		-webkit-align-items: center;
		-moz-align-items: center;
		-o-align-items: center;
		align-items: center;
		-webkit-justify-content: center;
		-moz-justify-content: center;
		-o-justify-content: center;
		justify-content: center;
	}
	.svgDivS,.svgDivF{
		margin: auto;
		width: 60px;
		height: 60px;
		padding: 20px;
		border-radius: 50%;
		margin-top: 60px;
	}
	.st0,.st1{
		fill:#fff;
	}
	/*.st0{fill:#0E9079;}
	.st1{fill:#C81431;}*/

	.successEffect .svgDivF,.failEffect .svgDivS{
		display: none;
	}
	.svgDivTit{
		margin-top: 20px;
		font-size: 3em;
		font-weight:bolder;
	}
	.successEffect .svgDivS {
		background: -webkit-linear-gradient(135deg,#4ae7d5,#35e688);
		background: -moz-linear-gradient(135deg,#4ae7d5,#35e688);
		background: -o-linear-gradient(135deg,#4ae7d5,#35e688);
		background: linear-gradient(135deg,#4ae7d5,#35e688);
		-webkit-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(70,218,201,0.3);
		-moz-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(70,218,201,0.3);
		-o-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(70,218,201,0.3);
		box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(70,218,201,0.3);
	}
	.successEffect .svgDivTit{
		color: #4ae7d5;
	}
	.failEffect .svgDivF{
		-webkit-background: linear-gradient(135deg,#da45a0,#da3b4a);
		-moz-background: linear-gradient(135deg,#da45a0,#da3b4a);
		-o-background: linear-gradient(135deg,#da45a0,#da3b4a);
		background: linear-gradient(135deg,#da45a0,#da3b4a);
		-webkit-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(236,75,173,0.2);
		-moz-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(236,75,173,0.2);
		-o-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(236,75,173,0.2);
		box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 10px 10px 5px rgba(236,75,173,0.2);
	}
	.failEffect .svgDivTit{
		color: #da3b4a;
	}
	.svgDivSysTit{
		margin-top: 10px;
		font-size: 0.8em;
		color:#bbb;
	}
	.svgDivSysCode{
		margin-top: 4px;
		display:inline-block;
		padding: 2px 20px;
		background: -webkit-linear-gradient(135deg,#4ae7d5,#35e688);
		background: -moz-linear-gradient(135deg,#4ae7d5,#35e688);
		background: -o-linear-gradient(135deg,#4ae7d5,#35e688);
		background: linear-gradient(135deg,#4ae7d5,#35e688);
		color: #eee;
		border-radius: 30px;
		font-size: 0.8em;
		-webkit-text-shadow: 0px 0px 8px rgba(0,0,0,0.3);
		-moz-text-shadow: 0px 0px 8px rgba(0,0,0,0.3);
		-o-text-shadow: 0px 0px 8px rgba(0,0,0,0.3);
		text-shadow: 0px 0px 8px rgba(0,0,0,0.3);
		letter-spacing: 2px;
	}
	.successEffect .svgDivSysCode{
		-webkit-background: linear-gradient(135deg,#4ae7d5,#35e688);
		-moz-background: linear-gradient(135deg,#4ae7d5,#35e688);
		-o-background: linear-gradient(135deg,#4ae7d5,#35e688);
		background: linear-gradient(135deg,#4ae7d5,#35e688);
		-wdebkit-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(70,218,201,0.3);
		-moz-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(70,218,201,0.3);
		-o-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(70,218,201,0.3);
		box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(70,218,201,0.3);
	}
	.failEffect .svgDivSysCode{
		-webkit-background: linear-gradient(135deg,#da45a0,#da3b4a);
		-moz-background: linear-gradient(135deg,#da45a0,#da3b4a);
		-o-background: linear-gradient(135deg,#da45a0,#da3b4a);
		background: linear-gradient(135deg,#da45a0,#da3b4a);
		-webkit-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(236,75,173,0.2);
		-moz-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(236,75,173,0.2);
		-o-box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(236,75,173,0.2);
		box-shadow: inset 0px 0px 10px 0px rgba(0,0,0,0.05) , 0px 5px 10px 5px rgba(236,75,173,0.2);
	}



	.BtnWrapper{
		width: 100%;
		height: 60px;
		position: absolute;
		left: 0px;
		bottom: 10px;
	}
	.closeBtn{
		margin: auto;
		display: block;
		width: 90%;
		height: 50px;
		line-height: 50px;
		-webkit-background: linear-gradient(135deg,#aaa,#bbb);
		-moz-background: linear-gradient(135deg,#aaa,#bbb);
		-o-background: linear-gradient(135deg,#aaa,#bbb);
		background: linear-gradient(135deg,#aaa,#bbb);
		border-radius: 6px;
		color: #fff;
		cursor: pointer;
		font-size: 1.2em;
		-webkit-box-shadow: inset 0px 0px 0px 0px rgba(255,255,255,0.0);
		-moz-box-shadow: inset 0px 0px 0px 0px rgba(255,255,255,0.0);
		-o-box-shadow: inset 0px 0px 0px 0px rgba(255,255,255,0.0);
		box-shadow: inset 0px 0px 0px 0px rgba(255,255,255,0.0);
		-o-transition: all .3s; -moz-transition: all .3s; -webkit-transition: all .3s; -ms-transition: all .3s; transition: all .3s;

	}
	.closeBtn:hover{
		-moz-background: linear-gradient(135deg,#777,#999);
		-webkit-background: linear-gradient(135deg,#777,#999);
		-o-background: linear-gradient(135deg,#777,#999);
		background: linear-gradient(135deg,#777,#999);
		-webkit-box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
		-moz-box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
		-o-box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
		box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
	}
	.closeBtn:active{
		-webkit-background: linear-gradient(135deg,#777,#999);
		-moz-background: linear-gradient(135deg,#777,#999);
		-o-background: linear-gradient(135deg,#777,#999);
		background: linear-gradient(135deg,#777,#999);
		-webkit-box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
		-moz-box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
		-o-box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
		box-shadow: inset 0px 0px 0px 3px rgba(255,255,255,0.5);
	}

	@media (min-width: 720px) {
		.mainInfo{
			position: relative;
			display: block;
			text-align: center;
			width: 400px;
			height: auto;
			border-radius: 10px;
			min-height: 350px;
		}

	}
	@media (max-width: 719px) {
		.mainInfo{
			position: relative;
			display: block;
			text-align: center;
			width: 90%;
			height: 80%;
			border-radius: 10px;
			min-height: 350px;
		}
	}
    </style>
	<script>
        window.onload = function () {
            var boolIsPaymentSuccess = '<%=IsPaymentSuccess %>';
            if (boolIsPaymentSuccess == 'True') {
				document.getElementById("mainInfo").classList.add("successEffect");
                document.getElementById("messageSpan").textContent ="交易完成";
                
            } else {
				document.getElementById("mainInfo").classList.add("failEffect");
                document.getElementById("messageSpan").textContent ="交易處理中";
            }
        }
    </script>
</head>
<body>
    <div class="mainWrapper">
        <div id="mainInfo" class="mainInfo">
            <!--成功時加上樣式"successEffect" 失敗時加上樣式"failEffect"-->
            <div class="svgDivS">
                <svg version="1.1" id="svgDivSS" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px"
                     viewBox="0 0 150 150" style="enable-background:new 0 0 150 150;" xml:space="preserve">
                <g>
                <path class="st0" d="M62.3,122.1c-2.3,0-4.6-1-6.2-2.7L17.3,78c-3.2-3.4-3-8.7,0.4-11.9c3.4-3.2,8.7-3,11.9,0.4l32.3,34.4L120.1,31
							c3-3.6,8.3-4.1,11.9-1.1c3.6,3,4.1,8.3,1.1,11.9L68.8,119c-1.6,1.9-3.8,3-6.2,3C62.4,122.1,62.4,122.1,62.3,122.1z" />

					</g>
					</svg>
            </div>
            <div class="svgDivF">
                <svg version="1.1" id="svgDivSF" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px"
                     viewBox="0 0 150 150" style="enable-background:new 0 0 150 150;" xml:space="preserve">
                <path class="st1" d="M86.9,75l32.8-32.8c3.3-3.3,3.3-8.7,0-11.9c-3.3-3.3-8.7-3.3-11.9,0L75,63.1L42.2,30.3c-3.3-3.3-8.7-3.3-11.9,0
						s-3.3,8.7,0,11.9L63,75l-32.8,32.8c-3.3,3.3-3.3,8.7,0,11.9c1.7,1.7,3.8,2.5,6,2.5c2.2,0,4.3-0.8,6-2.5L75,87l32.8,32.8
						c1.7,1.7,3.8,2.5,6,2.5c2.2,0,4.3-0.8,6-2.5c3.3-3.3,3.3-8.7,0-11.9L86.9,75z" />

					</svg>
            </div>
	
            <div class="svgDivTit"><span class="language_replace" id="messageSpan"></span></div>
           <%-- <div class="svgDivSysTit"><span class="language_replace">訊息代碼</span></div>
            <div class="svgDivSysCode">@ResultCode</div>--%>
 
        </div>
    </div>
</body>
</html>
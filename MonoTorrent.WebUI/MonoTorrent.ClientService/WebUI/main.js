var perSec="/s";function setupUI(){loadLangStrings();var B=function(E,D,C,F){return{text:E,type:D||TYPE_STRING,align:F||ALIGN_AUTO,disabled:!!C}};var A=utWebUI.config.trtCols;utWebUI.trtTable.create("List",[B(lang[CONST.OV_COL_NAME],TYPE_STRING,A&1),B(lang[CONST.OV_COL_STATUS],TYPE_STRING,A&2),B(lang[CONST.OV_COL_SIZE],TYPE_NUMBER,A&4),B(lang[CONST.OV_COL_DONE],TYPE_NUMBER,A&8),B(lang[CONST.OV_COL_DOWNLOADED],TYPE_NUMBER,A&16),B(lang[CONST.OV_COL_UPPED],TYPE_NUMBER,A&32),B(lang[CONST.OV_COL_SHARED],TYPE_NUMBER,A&64),B(lang[CONST.OV_COL_DOWNSPD],TYPE_NUMBER,A&128),B(lang[CONST.OV_COL_UPSPD],TYPE_NUMBER,A&256),B(lang[CONST.OV_COL_ETA],TYPE_NUMBER,A&512),B(lang[CONST.OV_COL_LABEL],TYPE_STRING,A&1024),B(lang[CONST.OV_COL_PEERS],TYPE_NUMBER,A&2048),B(lang[CONST.OV_COL_SEEDS],TYPE_NUMBER,A&4096),B(lang[CONST.OV_COL_AVAIL].split("||")[1],TYPE_NUMBER,A&8192),B(lang[CONST.OV_COL_ORDER],TYPE_NUM_ORDER,A&16384,ALIGN_RIGHT),B(lang[CONST.OV_COL_REMAINING],TYPE_NUMBER,A&32768)],$extend({format:function(D,E){var C=D.length;if(isNaN(E)){E=0}for(var F=0;F<C;F++){switch(E){case 0:case 1:case 10:case 11:case 12:break;case 2:D[F]=D[F].toFileSize(2);break;case 3:D[F]=(D[F]/10).roundTo(1)+"%";break;case 4:D[F]=D[F].toFileSize();break;case 5:D[F]=D[F].toFileSize();break;case 6:D[F]=(D[F]==-1)?"\u221E":(D[F]/1000).roundTo(3);break;case 7:D[F]=(D[F]>=103)?(D[F].toFileSize()+perSec):"";break;case 8:D[F]=(D[F]>=103)?(D[F].toFileSize()+perSec):"";break;case 9:D[F]=(D[F]==0)?"":(D[F]<=-1)?"\u221E":D[F].toTimeString();break;case 13:D[F]=(D[F]/65535).roundTo(3);break;case 14:D[F]=(D[F]<=-1)?"*":D[F];break;case 15:D[F]=D[F].toFileSize(2);break}E++}return D},onDelete:utWebUI.remove.bind(utWebUI),onColResize:utWebUI.trtColResize.bind(utWebUI),onColMove:utWebUI.trtColMove.bind(utWebUI),onColToggle:utWebUI.trtColToggle.bind(utWebUI),onSort:utWebUI.trtSort.bind(utWebUI),onSelect:utWebUI.trtSelect.bind(utWebUI)},utWebUI.config.torrentTable));if(!isGuest){A=utWebUI.config.flsCols;utWebUI.flsTable.create("FileList",[B(lang[CONST.FI_COL_NAME],TYPE_STRING,A&1),B(lang[CONST.FI_COL_SIZE],TYPE_NUMBER,A&2),B(lang[CONST.FI_COL_DONE],TYPE_NUMBER,A&4),B(lang[CONST.FI_COL_PCT],TYPE_NUMBER,A&8),B(lang[CONST.FI_COL_PRIO],TYPE_NUMBER,A&16)],$extend({format:function(D,E){var C=D.length;if(isNaN(E)){E=0}for(var F=0;F<C;F++){switch(E){case 0:break;case 1:D[F]=D[F].toFileSize(2);break;case 2:D[F]=D[F].toFileSize(2);break;case 3:D[F]=D[F]+"%";break;case 4:D[F]=lang[CONST["FI_PRI"+D[F]]]}E++}return D},onColResize:utWebUI.flsColResize.bind(utWebUI),onColMove:utWebUI.flsColMove.bind(utWebUI),onColToggle:utWebUI.trtColToggle.bind(utWebUI),onSort:utWebUI.flsSort.bind(utWebUI),onSelect:utWebUI.flsSelect.bind(utWebUI),onRefresh:function(){if(this.torrentID!=""){utWebUI.getFiles(utWebUI.torrentID,true)}},refreshable:true},utWebUI.config.fileTable));utWebUI.flsTable.loadObj.hide()}resizeUI();["_all_","_dls_","_com_","_act_","_iac_","_nlb_"].each(function(C){$(C).addEvent("click",function(){utWebUI.switchLabel(this)})});if(isGuest){return }$("query").addEvent("keydown",function(C){if(C.code==13){Search()}});new Drag("HDivider",{modifiers:{x:"left",y:""},onComplete:function(){resizeUI.delay(20,null,[window.getSize().x-this.value.now.x,null])}});new Drag("VDivider",{modifiers:{x:"",y:"top"},onComplete:function(){resizeUI.delay(20,null,[null,this.value.now.y])}})}function checkProxySettings(){var B=$("proxy.auth").checked;var A=$("proxy.type").get("value").toInt();if(A==0){$("proxy.username").disabled=$("proxy.password").disabled=true}else{if(A==1){if(B){$("proxy.username").disabled=false;$("proxy.password").disabled=true;$("DLG_SETTINGS_4_CONN_18").addClass("disabled")}}else{if(A==4){$("proxy.p2p").disabled=true;$("DLG_SETTINGS_4_CONN_20").addClass("disabled")}}}if((A>1)&&B){$("proxy.username").disabled=false;$("proxy.password").disabled=false;$("DLG_SETTINGS_4_CONN_16").removeClass("disabled");$("DLG_SETTINGS_4_CONN_18").removeClass("disabled")}}function checkUpload(B){var A=$("torrent_file").get("value");if(!A.test(/\.torrent$/)){alert("The file has to be a torrent file.");return false}$("ADD_FILE_OK").disabled=true;return true}function Search(){window.open(searchList[searchActive][1]+""+$("query").get("value"),"_blank")}function log(E){var D=new Date();var C=D.getHours();var A=D.getMinutes();var B=D.getSeconds();C=(C<10)?("0"+C):C;A=(A<10)?("0"+A):A;B=(B<10)?("0"+B):B;$("lcont").grab(new Element("br"),"top").appendText("["+C+":"+A+":"+B+"] "+E,"top")}var searchList=[["Mininova","http://www.mininova.org/search/?utorrent&search="],["BitTorrent","http://search.bittorrent.com/search.jsp?Submit2=Search&query="],["The Pirate Bay","http://thepiratebay.org/search.php?q="],["TorrentSpy","http://torrentspy.com/search.asp?query="],["IsoHunt","http://isohunt.com/torrents.php?ext=&op=and&ihq="],["PointBlank","http://bt.point-blank.cc/?search="],["orb_bt","http://www.orbdesign.net/bt/results.php?sitefilter=1&query="],[],["Google","http://google.com/search?q="]];var searchActive=0;function searchSet(A){searchActive=A;$("query").focus()}function loadLangStrings(){["OV_CAT_ALL","OV_CAT_DL","OV_CAT_COMPL","OV_CAT_ACTIVE","OV_CAT_INACTIVE","OV_CAT_NOLABEL"].each(function(D){$(D).set("text",lang[CONST[D]])});if(isGuest){return }var C=lang[CONST.OV_TABS].split("||");utWebUI.tabs=new Tabs($("tabs"),{tabs:{gcont:C[0],FileList:C[4],lcont:C[6]},onChange:utWebUI.tabChange.bind(utWebUI)}).draw().show("gcont");["DLG_TORRENTPROP_1_GEN_01","DLG_TORRENTPROP_1_GEN_03","DLG_TORRENTPROP_1_GEN_04","DLG_TORRENTPROP_1_GEN_06","DLG_TORRENTPROP_1_GEN_08","DLG_TORRENTPROP_1_GEN_10","DLG_TORRENTPROP_1_GEN_11","DLG_TORRENTPROP_1_GEN_12","DLG_TORRENTPROP_1_GEN_14","DLG_TORRENTPROP_1_GEN_16","DLG_TORRENTPROP_1_GEN_17","DLG_TORRENTPROP_1_GEN_18","DLG_TORRENTPROP_1_GEN_19","GN_TRANSFER","GN_TP_01","GN_TP_02","GN_TP_03","GN_TP_04","GN_TP_05","GN_TP_06","GN_TP_07","GN_TP_08","OV_NEWLABEL_TEXT"].each(function(D){$(D).set("text",lang[CONST[D]])});[["dlgProps-header",CONST.DLG_TORRENTPROP_00],["dlgLabel-header",CONST.OV_NEWLABEL_CAPTION],["dlgSettings-header",CONST.DLG_SETTINGS_00],["dlgAdd-header",CONST.OV_TB_ADDTORR]].each(function(D){$(D[0]).set("text",lang[D[1]])});var B=$("prop-seed_time"),A=$("seed_time");B.options.length=A.options.length=0;[0,5400,7200,10800,14400,18000,21600,25200,28800,32400,36000,43200,57600,72000,86400,108000,129600,172800,216000,259200,345600].each(function(D){var E="";if(D==0){E=lang[CONST.ST_SEEDTIMES_IGNORE]}else{if(D==5400){E=lang[CONST.ST_SEEDTIMES_MINUTES].replace(/%d/,90)}else{E=lang[CONST.ST_SEEDTIMES_HOURS].replace(/%d/,D/3600)}}B.options[B.options.length]=new Option(E,D,false,D==0);A.options[A.options.length]=new Option(E,D,false,D==0)});$("DLG_TORRENTPROP_01").set("value",lang[CONST.DLG_TORRENTPROP_01]).addEvent("click",function(){$("dlgProps").hide();utWebUI.setProperties()});$("DLG_TORRENTPROP_02").set("value",lang[CONST.DLG_TORRENTPROP_02]).addEvent("click",function(){$("dlgProps").hide()});$("LBL_OK").set("value",lang[CONST.DLG_SETTINGS_03]).addEvent("click",function(){$("dlgLabel").hide();utWebUI.createLabel()});$("LBL_CANCEL").set("value",lang[CONST.DLG_SETTINGS_04]).addEvent("click",function(){$("dlgLabel").hide()});$("ADD_FILE_OK").set("value",lang[CONST.DLG_SETTINGS_03]);$("ADD_FILE_CANCEL").set("value",lang[CONST.DLG_SETTINGS_04]).addEvent("click",function(){$("dlgAdd").hide()});$("ADD_URL_OK").set("value",lang[CONST.DLG_SETTINGS_03]).addEvent("click",function(){$("dlgAdd").hide();utWebUI.addURL()});$("ADD_URL_CANCEL").set("value",lang[CONST.DLG_SETTINGS_04]).addEvent("click",function(){$("dlgAdd").hide()});["remove","start","pause","stop"].each(function(D){$(D).setProperty("title",lang[CONST["OV_TB_"+D.toUpperCase()]])});$("setting").setProperty("title",lang[CONST.OV_TB_PREF]);$("add").setProperty("title",lang[CONST.OV_TB_ADDTORR]);perSec="/"+lang[CONST.TIME_SECS].replace(/%d/,"").trim()}function loadSettingStrings(){new Tabs($("stgmenu"),{tabs:{st_webui:lang[CONST.ST_CAPT_WEBUI],st_gl:lang[CONST.ST_CAPT_GENERAL],st_dirs:lang[CONST.ST_CAPT_FOLDER],st_con:lang[CONST.ST_CAPT_CONNECTION],st_bw:lang[CONST.ST_CAPT_BANDWIDTH],st_bt:lang[CONST.ST_CAPT_TRANSFER],st_que:lang[CONST.ST_CAPT_SEEDING],st_sch:lang[CONST.ST_CAPT_SCHEDULER],st_ao:lang[CONST.ST_CAPT_ADVANCED],st_dc:lang[CONST.ST_CAPT_DISK_CACHE]}}).draw().show("st_webui");["DLG_SETTINGS_1_GENERAL_02","DLG_SETTINGS_1_GENERAL_10","DLG_SETTINGS_1_GENERAL_11","DLG_SETTINGS_1_GENERAL_12","DLG_SETTINGS_1_GENERAL_13","DLG_SETTINGS_1_GENERAL_17","DLG_SETTINGS_1_GENERAL_18","DLG_SETTINGS_1_GENERAL_19","DLG_SETTINGS_1_GENERAL_20","DLG_SETTINGS_2_UI_02","DLG_SETTINGS_2_UI_05","DLG_SETTINGS_2_UI_06","DLG_SETTINGS_2_UI_15","DLG_SETTINGS_2_UI_16","DLG_SETTINGS_3_PATHS_01","DLG_SETTINGS_3_PATHS_02","DLG_SETTINGS_3_PATHS_06","DLG_SETTINGS_3_PATHS_07","DLG_SETTINGS_3_PATHS_10","DLG_SETTINGS_3_PATHS_11","DLG_SETTINGS_3_PATHS_12","DLG_SETTINGS_3_PATHS_15","DLG_SETTINGS_3_PATHS_18","DLG_SETTINGS_3_PATHS_19","DLG_SETTINGS_4_CONN_01","DLG_SETTINGS_4_CONN_02","DLG_SETTINGS_4_CONN_05","DLG_SETTINGS_4_CONN_06","DLG_SETTINGS_4_CONN_07","DLG_SETTINGS_4_CONN_08","DLG_SETTINGS_4_CONN_09","DLG_SETTINGS_4_CONN_11","DLG_SETTINGS_4_CONN_13","DLG_SETTINGS_4_CONN_15","DLG_SETTINGS_4_CONN_16","DLG_SETTINGS_4_CONN_18","DLG_SETTINGS_4_CONN_20","DLG_SETTINGS_4_CONN_21","DLG_SETTINGS_5_BANDWIDTH_01","DLG_SETTINGS_5_BANDWIDTH_02","DLG_SETTINGS_5_BANDWIDTH_03","DLG_SETTINGS_5_BANDWIDTH_05","DLG_SETTINGS_5_BANDWIDTH_07","DLG_SETTINGS_5_BANDWIDTH_08","DLG_SETTINGS_5_BANDWIDTH_10","DLG_SETTINGS_5_BANDWIDTH_11","DLG_SETTINGS_5_BANDWIDTH_14","DLG_SETTINGS_5_BANDWIDTH_15","DLG_SETTINGS_5_BANDWIDTH_17","DLG_SETTINGS_6_BITTORRENT_01","DLG_SETTINGS_6_BITTORRENT_02","DLG_SETTINGS_6_BITTORRENT_03","DLG_SETTINGS_6_BITTORRENT_04","DLG_SETTINGS_6_BITTORRENT_05","DLG_SETTINGS_6_BITTORRENT_06","DLG_SETTINGS_6_BITTORRENT_07","DLG_SETTINGS_6_BITTORRENT_08","DLG_SETTINGS_6_BITTORRENT_10","DLG_SETTINGS_6_BITTORRENT_11","DLG_SETTINGS_6_BITTORRENT_13","DLG_SETTINGS_7_QUEUEING_01","DLG_SETTINGS_7_QUEUEING_02","DLG_SETTINGS_7_QUEUEING_04","DLG_SETTINGS_7_QUEUEING_06","DLG_SETTINGS_7_QUEUEING_07","DLG_SETTINGS_7_QUEUEING_09","DLG_SETTINGS_7_QUEUEING_11","DLG_SETTINGS_7_QUEUEING_12","DLG_SETTINGS_7_QUEUEING_13","DLG_SETTINGS_8_SCHEDULER_01","DLG_SETTINGS_8_SCHEDULER_04","DLG_SETTINGS_8_SCHEDULER_05","DLG_SETTINGS_8_SCHEDULER_07","DLG_SETTINGS_8_SCHEDULER_09","DLG_SETTINGS_9_WEBUI_01","DLG_SETTINGS_9_WEBUI_02","DLG_SETTINGS_9_WEBUI_03","DLG_SETTINGS_9_WEBUI_05","DLG_SETTINGS_9_WEBUI_07","DLG_SETTINGS_9_WEBUI_09","DLG_SETTINGS_9_WEBUI_10","DLG_SETTINGS_9_WEBUI_12","DLG_SETTINGS_A_ADVANCED_01","DLG_SETTINGS_B_ADV_UI_07","DLG_SETTINGS_C_ADV_CACHE_01","DLG_SETTINGS_C_ADV_CACHE_02","DLG_SETTINGS_C_ADV_CACHE_03","DLG_SETTINGS_C_ADV_CACHE_05","DLG_SETTINGS_C_ADV_CACHE_06","DLG_SETTINGS_C_ADV_CACHE_07","DLG_SETTINGS_C_ADV_CACHE_08","DLG_SETTINGS_C_ADV_CACHE_09","DLG_SETTINGS_C_ADV_CACHE_10","DLG_SETTINGS_C_ADV_CACHE_11","DLG_SETTINGS_C_ADV_CACHE_12","DLG_SETTINGS_C_ADV_CACHE_13","DLG_SETTINGS_C_ADV_CACHE_14","DLG_SETTINGS_C_ADV_CACHE_15","MENU_SHOW_CATEGORY","MENU_SHOW_DETAIL","ST_COL_NAME","ST_COL_VALUE"].each(function(C){$(C).set("text",lang[CONST[C]])});$("DLG_SETTINGS_03").set("value",lang[CONST.DLG_SETTINGS_03]).addEvent("click",function(){$("dlgSettings").hide();utWebUI.setSettings()});$("DLG_SETTINGS_04").set("value",lang[CONST.DLG_SETTINGS_04]).addEvent("click",function(){$("dlgSettings").hide();utWebUI.loadSettings()});$("DLG_SETTINGS_4_CONN_04").set("value",lang[CONST.DLG_SETTINGS_4_CONN_04]).addEvent("click",function(){var C=utWebUI.settings.bind_port,D=0;do{D=parseInt(Math.random()*50000)+15000}while(C==D);$("bind_port").set("value",D)});var B=$("encryption_mode");B.options.length=0;lang[CONST.ST_CBO_ENCRYPTIONS].split("||").each(function(D,C){if(D==""){return }B.options[B.options.length]=new Option(D,C,false,false)});B.set("value",utWebUI.settings.encryption_mode);var A=$("proxy.type");A.options.length=0;lang[CONST.ST_CBO_PROXY].split("||").each(function(D,C){if(D==""){return }A.options[A.options.length]=new Option(D,C,false,false)});A.set("value",utWebUI.settings["proxy.type"]);utWebUI.langLoaded=true}var resizing=false,resizeTimeout=null;function resizeUI(L,H){resizing=true;$clear(resizeTimeout);var M=window.getSize();var G=M.x,B=M.y,J=false;var D=utWebUI.config.showCategories,A=utWebUI.config.showDetails,I=utWebUI.config.showToolbar,E=0;if(!isGuest&&I){E=$("toolbar").getSize().y}if(!L&&!H){L=Math.floor(G*((D)?utWebUI.config.hSplit:1));H=Math.floor(B*((!isGuest&&A)?utWebUI.config.vSplit:1));J=true}if(L){L-=D?10:2}if(H){H-=E+((A&&I)?5:I?8:2)}if(D){if(L){$("CatList").setStyle("width",G-10-L-((Browser.Engine.trident&&!Browser.Engine.trident5)?4:0))}if(H){$("CatList").setStyle("height",H)}}if(!isGuest&&A){$("tdetails").setStyle("width",G-(Browser.Engine.trident4?14:12));if(H){var C=B-H,K=C-(I?46:41)-E;$("tdetails").setStyle("height",C-10);$("tdcont").setStyle("height",K);$("gcont").setStyle("height",K-8);$("lcont").setStyle("height",K-12);utWebUI.flsTable.resizeTo(G-22,K-2)}}utWebUI.trtTable.resizeTo(L,H);if(isGuest){return }var F=$("List").getPosition();$("HDivider").setStyle("left",F.x-((Browser.Engine.trident&&!Browser.Engine.trident5)?7:5));$("VDivider").setStyle("width",G+(Browser.Engine.trident6?4:0));if(H){$("HDivider").setStyles({height:D?(H+2):0,top:I?43:0});$("VDivider").setStyle("top",A?(F.y+H+(!Browser.Engine.trident6?2:0)):-10);if(A&&!J){utWebUI.config.vSplit=H/(B-E-12)}}if(L&&D&&!J){utWebUI.config.hSplit=L/G}resizing=false}function linked(F,B,H,A,I){A=A||[];var D=true,K=F.get("tag");if(K=="input"){if(F.type=="checkbox"){D=!F.checked||F.disabled}if(I){D=!D}}else{if(K=="select"){D=(F.get("value")==B)}else{return }}var G;for(var E=0,C=H.length;E<C;E++){if(!(G=$(H[E]))){continue}if(G.type!="checkbox"){G[(D?"add":"remove")+"Class"]("disabled")}G.disabled=D;G.fireEvent(((K=="input")&&Browser.Engine.trident)?"click":"change");if(A.contains(H[E])){continue}var J=G.getPrevious();if(!J||(J.get("tag")!="label")){J=G.getNext();if(!J||(J.get("tag")!="label")){continue}}J[(D?"add":"remove")+"Class"]("disabled")}}var winZ=500;window.addEvent("domready",function(){$(document.body);document.title="\u00B5Torrent WebUI "+VERSION;if(isGuest){utWebUI.init();return }document.addEvent("keydown",function(D){switch(D.key){case"esc":D.stop();utWebUI.restoreUI();break;case"a":if(D.control){D.stop()}break;case"e":if(D.control){D.stop()}break;case"o":if(D.control){D.stop();$("dlgAdd").setStyle("zIndex",++winZ).centre()}break;case"p":if(D.control){D.stop();utWebUI.showSettings()}break;case"f2":D.stop();$("dlgAbout").setStyle("zIndex",++winZ).centre();break;case"f4":D.stop();utWebUI.toggleToolbar();break;case"f6":D.stop();utWebUI.toggleDetPanel();break;case"f7":D.stop();utWebUI.toggleCatPanel();break}});if(Browser.Engine.presto){document.addEvent("keypress",function(D){switch(D.key){case"esc":D.stop();break;case"a":if(D.control){D.stop()}break;case"e":if(D.control){D.stop()}break;case"o":if(D.control){D.stop()}break;case"p":if(D.control){D.stop()}break;case"f2":D.stop();break;case"f4":D.stop();break;case"f6":D.stop();break;case"f7":D.stop();break}})}window.addEvent("unload",function(){utWebUI.saveConfig()});window.addEvent("resize",function(){if(resizing){return }if(Browser.Engine.trident&&!resizing){$clear(resizeTimeout);resizeTimeout=resizeUI.delay(100)}else{resizeUI()}});document.addEvent("mousedown",function(D){if((D.rightClick&&!ContextMenu.launched)||(!D.rightClick&&!ContextMenu.hidden&&!ContextMenu.focused)){ContextMenu.hide.delay(10,ContextMenu)}ContextMenu.launched=false});if(Browser.Engine.gecko){document.addEvent("mousedown",function(D){if(D.rightClick&&!(/^input|textarea|a$/i).test(D.target.tagName)){D.stop()}}).addEvent("click",function(D){if(D.rightClick&&!(/^input|textarea|a$/i).test(D.target.tagName)){D.stop()}})}if(Browser.Engine.presto&&!("oncontextmenu" in document.createElement("foo"))){var C;document.addEvent("mousedown",function(E){if(!E.rightClick){return }var D=E.target;while(D){if(!C){var F=E.target.ownerDocument;C=F.createElement("input");C.type="button";(F.body||F.documentElement).appendChild(C);C.style.cssText="z-index: 1000;position:absolute;top:"+(E.client.y-2)+"px;left:"+(E.client.x-2)+"px;width:5px;height:5px;opacity:0.01"}D=D.parentNode}});document.addEvent("mouseup",function(D){if(C){C.parentNode.removeChild(C);C=undefined;if(D.rightClick&&!(/^input|textarea|a$/i).test(D.target.tagName)){D.stop();return false}}})}else{if(Browser.Engine.trident||Browser.Engine.webkit){document.addEvent("contextmenu",function(D){if(!(/^input|textarea|a$/i).test(D.target.tagName)){D.stop();return false}return true})}}$("search").addEvent("click",function(F){F.stop();ContextMenu.clear();for(var E=0,D=searchList.length;E<D;E++){if(searchList[E].length==0){ContextMenu.add([CMENU_SEP])}else{if(E==searchActive){ContextMenu.add([CMENU_SEL,searchList[E][0]])}else{ContextMenu.add([searchList[E][0],searchSet.pass(E)])}}}var G=this.getPosition();G.x-=8;G.y+=14;ContextMenu.show(G)});new IFrame({id:"uploadfrm",src:"about:blank",onload:function(E){$("torrent_file").set("value","");$("ADD_FILE_OK").disabled=false;var F=$(E.body).get("html");if(F!=""){var D=JSON.decode(F);if(has(D,"error")){alert(D.error)}}}}).inject(document.body);$("upfrm").addEvent("submit",function(){return checkUpload(this)});ContextMenu.init("ContextMenu");$("add").addEvent("click",function(D){D.stop();$("dlgAdd").setStyle("zIndex",++winZ).centre()});["remove","start","pause","stop"].each(function(D){$(D).addEvent("click",function(E){E.stop();utWebUI[D]()})});$("setting").addEvent("click",function(D){D.stop();utWebUI.showSettings()});var B=$("dragmask");["dlgAdd","dlgSettings","dlgProps","dlgAbout","dlgLabel"].each(function(E){$(E).addEvent("mousedown",function(G){var F=G.target.className;if(F.contains("dlg-header"," ")||F.contains("dlg-close"," ")){return }this.setStyle("zIndex",++winZ)}).getElement("a").addEvent("click",function(F){F.stop();$(E).hide()});var D=null;new Drag(E,{handle:E+"-header",modifiers:{x:"left",y:"top"},snap:2,onBeforeStart:function(){var F=this.element.getSize(),G=this.element.getPosition();B.setStyles({width:F.x-4,height:F.y-4,left:G.x,top:G.y,zIndex:++winZ});D=this.element;this.element=B},onStart:function(){this.element.show()},onCancel:function(){this.element=D;this.element.setStyle("zIndex",++winZ);B.setStyle("display","none")},onComplete:function(){this.element=D;var F=B.getPosition();B.setStyle("display","none");this.element.setStyles({left:F.x,top:F.y,zIndex:++winZ})}})});var A=Browser.Engine.trident?"click":"change";$("proxy.type").addEvent("change",function(){linked(this,0,["proxy.proxy","proxy.port","proxy.auth","proxy.p2p"]);checkProxySettings()});$("proxy.auth").addEvent(A,function(){linked(this,0,["proxy.username","proxy.password"]);checkProxySettings()});$("cache.override").addEvent(A,function(){linked(this,0,["cache.override_size"],["cache.override_size"])});$("cache.write").addEvent(A,function(){linked(this,0,["cache.writeout","cache.writeimm"])});$("cache.read").addEvent(A,function(){linked(this,0,["cache.read_turnoff","cache.read_prune","cache.read_trash"])});$("prop-seed_override").addEvent(A,function(){linked(this,0,["prop-seed_ratio","prop-seed_time"])});$("webui.enable_guest").addEvent(A,function(){linked(this,0,["webui.guest"])});$("webui.enable_listen").addEvent(A,function(){linked(this,0,["webui.port"])});$("seed_prio_limitul_flag").addEvent(A,function(){linked(this,0,["seed_prio_limitul"])});$("sched_enable").addEvent(A,function(){linked(this,0,["sched_ul_rate","sched_dl_rate","sched_dis_dht"])});$("dir_active_download_flag").addEvent(A,function(){linked(this,0,["dir_active_download"])});$("dir_completed_download_flag").addEvent(A,function(){linked(this,0,["dir_add_label","dir_completed_download","move_if_defdir"])});$("dir_torrent_files_flag").addEvent(A,function(){linked(this,0,["dir_torrent_files"])});$("dir_completed_torrents_flag").addEvent(A,function(){linked(this,0,["dir_completed_torrents"])});$("dir_autoload_flag").addEvent(A,function(){linked(this,0,["dir_autoload_delete","dir_autoload"])});$("ul_auto_throttle").addEvent(A,function(){linked(this,0,["max_ul_rate","max_ul_rate_seed_flag"],["max_ul_rate"],true)});$("max_ul_rate_seed_flag").addEvent(A,function(){linked(this,0,["max_ul_rate_seed"])});utWebUI.init()});
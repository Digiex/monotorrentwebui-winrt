function has(B,A){return Object.prototype.hasOwnProperty.apply(B,[A])}function redirect(A){window.location.href=A}window.onerror=function(C,B,A){log("JS error: ["+A+"] "+C);return true};function loadJS(F,D){D=$extend({onload:$empty,document:document,check:$lambda(true)},D);var B=new Element("script",{src:F,type:"text/javascript",charset:"utf-8"});var E=D.onload.bind(B),A=D.check,G=D.document;delete D.onload;delete D.check;delete D.document;B.addEvents({load:E,readystatechange:function(){if(Browser.Engine.trident&&["loaded","complete"].contains(this.readyState)){E()}}}).setProperties(D);if(Browser.Engine.webkit419){var C=(function(){if(!$try(A)){return }$clear(C);E()}).periodical(50)}return B.inject(G.head)}Array.implement({binarySearch:function(E,A,F,D){if(typeof A!="function"){A=function(H,G){if(H===G){return 0}if(H<G){return -1}return 1}}F=F||0;D=D||this.length;while(F<D){var C=parseInt((F+D)/2);var B=A(E,this[C]);if(B<0){D=C}else{if(B>0){F=C+1}else{return C}}}return -(F+1)},insertAt:function(B,A){this.splice(A,0,B);return this},swap:function(C,B){var A=this[C];this[C]=this[B];this[B]=A;return this},remove:function(B){for(var A=this.length;A--;){if(this[A]===B){this.splice(A,1);return A}}return -1}});String.implement({pad:function(A,D,B){var C=this;D=D||" ";B=B||"right";A-=C.length;if(A<0){return C}D=(new Array(Math.ceil(A/D.length)+1)).join(D).substr(0,A);return((B=="left")?(D+C):(C+D))}});Number.implement({toFileSize:function(A){A=A||1;var C=[lang[CONST.SIZE_KB],lang[CONST.SIZE_MB],lang[CONST.SIZE_GB]];var B=this;var D=0;B/=1024;while((B>=1024)&&(D<2)){B/=1024;D++}return(B.roundTo(A)+" "+C[D])},toTimeString:function(){var E=this;if(E>63072000){return"\u221E"}var A,G,H,F,D,C,I,B="";G=(E/31536000).toInt();A=E%31536000;H=(A/604800).toInt();A=A%604800;F=(A/86400).toInt();A=A%86400;D=(A/3600).toInt();A=A%3600;C=(A/60).toInt();I=A%60;if((G>0)&&(H>=0)){B=lang[CONST.TIME_YEARS_WEEKS].replace(/%d/,G).replace(/%d/,H)}else{if((H>0)&&(F>=0)){B=lang[CONST.TIME_WEEKS_DAYS].replace(/%d/,H).replace(/%d/,F)}else{if((D>0)&&(C>=0)){B=lang[CONST.TIME_HOURS_MINS].replace(/%d/,D).replace(/%d/,C)}else{if(C>0){B=lang[CONST.TIME_MINS_SECS].replace(/%d/,C).replace(/%d/,I)}else{B=lang[CONST.TIME_SECS].replace(/%d/,I)}}}}return B},roundTo:function(A){var B=""+this.round(A);var C=B.indexOf(".");if(C==-1){C=B.length;B+="."}return B.pad(A+ ++C,"0")}});
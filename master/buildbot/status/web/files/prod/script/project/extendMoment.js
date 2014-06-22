define(["moment","helpers"],function(e){var t,r=void 0,n=60,s=60*n;return e.fn.fromServerNow=function(){return void 0!==r?this.subtract(r).fromNow():this.fromNow()},t={init:function(){var r=t.getRelativeTimeDict();r.future="ETA: %s",r.past="Overtime: %s",e.lang("progress-bar-en",{relativeTime:r}),r=t.getRelativeTimeDict(),r.future="%s",r.past="Elapsed: %s",e.lang("progress-bar-no-eta-en",{relativeTime:r}),r=t.getRelativeTimeDict(),r.past="%s",r.future="%s",e.lang("waiting-en",{relativeTime:r}),e.lang("en")},setServerTime:function(t){r=e(t).diff(new Date),console.log("Time Offset: {0}".format(r))},getRelativeTimeDict:function(){return{s:"%d seconds",m:t.parseMinutesSeconds,mm:t.parseMinutesSeconds,h:t.parseHoursMinutes,hh:t.parseHoursMinutes,d:"a day",dd:"%d days",M:"a month",MM:"%d months",y:"a year",yy:"%d years"}},parseMinutesSeconds:function(e,t,r,s,o){var a=parseInt(o.seconds),i=a%n;if(n>a)return"{0} seconds".format(i);if(2*n>a)return"1 minute, {0} seconds".format(i);var u=Math.floor(a/n);return"{0} minutes, {1} seconds".format(u,i)},parseHoursMinutes:function(e,t,r,o,a){var i=parseInt(a.seconds),u=Math.floor(i%s/n);if(s>i){var f=Math.floor(i/n),m=i%n;return"{0} minutes, {1} seconds".format(f,m)}if(2*s>i)return"1 hour, {0} minutes".format(u);var d=Math.floor(i/s);return"{0} hours, {1} minutes".format(d,u)},getServerTime:function(r){return void 0===r?e().add(t.getServerOffset()):e(r).add(t.getServerOffset())},getServerOffset:function(){return r},getDateFormatted:function(t){return e.unix(t).format("MMMM DD, H:mm:ss")}}});
//# sourceMappingURL=extendMoment.js.map
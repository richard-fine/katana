define(["jquery","realtimePages","helpers","popup","handlebars","mustache","text!templates/build.handlebars","text!templates/builders.mustache","timeElements","popup"],function(e,t,n,r,i,s,o,u,a,f){var l,c=Handlebars.compile(o),h=!1,p=!1;Handlebars.registerHelper("buildCSSClass",function(e){return n.getCssClassFromStatus(e)});var d={updateArtifacts:function(t){var n=e("#artifacts-js").empty(),r={},i={},s;e.each(t.steps,function(t,n){n.urls!==undefined&&e.each(n.urls,function(e,t){typeof t=="string"&&(r[e]=t)})}),e.each(t.logs,function(e,t){t.length===2&&(t[1].indexOf(".xml")>-1||t[1].indexOf(".html")>-1)&&(i[t[0]]=t[1])}),r===undefined||Object.keys(r).length===0?n.html("No artifacts"):(s='<a class="artifact-popup artifacts-js more-info" href="#">Artifacts ({0})&nbsp;</a>'.format(Object.keys(r).length),n.html(s),f.initArtifacts(r,n.find(".artifact-popup"))),Object.keys(i).length>0&&(s="<li>Test Results</li>",e.each(i,function(e,t){s+='<li class="s-logs-js"><a href="{0}">{1}</a></li>'.format(t,e)}),s=e("<ul/>").addClass("tests-summary-list").html(s),n.append(s))}};return l={init:function(){var r=t.defaultRealtimeFunctions();r.build=l.processBuildDetailPage,t.initRealtime(r),a.setHeartbeat(1e3);if(window.location.search!==""){var i=e(".top");n.codeBaseBranchOverview(i)}},processBuildDetailPage:function(t){var n=Object.keys(t);n.length===1&&(t=t[n[0]]);var r=t.times[0],i=t.times[1],s=i!==null,o=t.eta;l.refreshIfRequired(s),l.processBuildResult(t,r,o,s),l.processSteps(t),d.updateArtifacts(t),i===null&&a.addElapsedElem(e("#elapsedTimeJs"),r),a.updateTimeObjects()},processBuildResult:function(t,r,i,o){var f=e("#buildResult");a.clearTimeObjects(f);var l="";i!==0&&(l=s.render(u,{progressBar:!0,etaStart:r,etaCurrent:i}));var h={buildResults:!0,b:t,buildIsFinished:o,progressBar:l},p=c(h);f.html(p);var d=f.find(".percent-outer-js");d.addClass("build-detail-progress"),n.delegateToProgressBar(d)},processSteps:function(t){var r="",i=e("#stepList"),s=1;e.each(t.steps,function(e,t){if(t.hidden)return!0;var i=t.isStarted,o=t.isFinished,u=t.results[0];i?i&&!o&&(u=8):u=9;var a=n.getCssClassFromStatus(u),f=t.times[0],l=t.times[1],h=n.getTime(f,l),p={step:!0,index:s,stepStarted:t.isStarted,run_time:h,css_class:a,s:t,url:t.url};return r+=c(p),s+=1,!0}),i.html(r)},refreshIfRequired:function(e){!p&&h&&e&&(window.location=window.location+"#finished",window.location.reload()),p===!1&&(p=e),h=!0}},l});
require.config({paths:{jquery:"libs/jQuery-2-0-3",selectors:"project/selectors",select2:"plugins/select2","datatables-plugin":"plugins/jquery-datatables",dataTables:"project/dataTables",dotdotdot:"plugins/jquery-dotdotdot",realtime:"project/realtime"}}),require(["jquery","project/set-current-item","project/popup","project/screen-size","project/project-drop-down","project/helpers"],function(e,t,n,r,i,s){e(document).ready(function(){t.init(),e(".tablesorter-js").length>0&&require(["dataTables"],function(e){e.init()}),require(["realtime"],function(e){e.init()}),e(".ellipsis-js").length&&require(["dotdotdot"],function(t){e(".ellipsis-js").dotdotdot()}),(e("#commonBranch_select").length||e(".select-tools-js").length)&&require(["selectors"],function(e){e.comboBox(".select-tools-js"),e.init()}),n.init(),i.init(),s.init()})});
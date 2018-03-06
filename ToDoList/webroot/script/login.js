$(document).ready(function() {
	if (window.location.href.indexOf("?loginfailed") > 0) {
		$("#loginErrorMsg").removeClass("hide");
	}
});
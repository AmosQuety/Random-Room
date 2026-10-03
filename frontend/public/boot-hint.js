// If the app script never arrives (a dropped connection or a content filter), say so instead of loading forever.
// React replaces the status line on mount, so this only ever shows when the app did not start.
setTimeout(function () {
  var status = document.getElementById("boot-status");
  if (status) status.textContent = "This is taking longer than usual. Check your connection, then reload the page.";
}, 15000);

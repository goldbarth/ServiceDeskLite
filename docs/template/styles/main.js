// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Close navbar when page loads.
// Without this, the Bootstrap navbar opens automatically when the page loads
// because the CSS class “in” is set incorrectly.
document.addEventListener("DOMContentLoaded", function () {
    var navbar = document.getElementById("navbar");
    if (navbar) {
        navbar.classList.remove("in");
    }
});

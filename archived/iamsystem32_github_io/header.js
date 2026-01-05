
document.addEventListener('DOMContentLoaded', function () {
    loadFooter();
});

function loadFooter() {
    fetch('/header.html')
        .then(response => { return response.text(); })
        .then(html => { document.querySelector('.fullscreen-container').insertAdjacentHTML('beforeend', html); })
}
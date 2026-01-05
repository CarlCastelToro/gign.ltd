
document.addEventListener('DOMContentLoaded', function () {
    loadFooter();
});

function loadFooter() {
    fetch('/footer.html')
        .then(response => { return response.text(); })
        .then(html => { document.querySelector('.fullscreen-container').insertAdjacentHTML('beforeend', html); })
}
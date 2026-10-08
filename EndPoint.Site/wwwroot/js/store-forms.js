// Shared by customer and Admin forms. No password value is read or copied.
(() => {
    document.querySelectorAll('input[type="password"]').forEach((input, index) => {
        if (!input.id) input.id = 'store-password-' + index;
        const wrapper = document.createElement('div');
        wrapper.className = 'store-password';
        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-outline-secondary store-password-toggle';
        button.setAttribute('aria-controls', input.id);
        button.setAttribute('aria-pressed', 'false');
        button.textContent = 'نمایش رمز';
        button.addEventListener('click', () => {
            const visible = input.type === 'password';
            input.type = visible ? 'text' : 'password';
            button.setAttribute('aria-pressed', String(visible));
            button.textContent = visible ? 'پنهان کردن رمز' : 'نمایش رمز';
        });
        wrapper.appendChild(button);
    });
    // Existing AJAX authentication/Admin forms use a same-origin antiforgery header.
    if (window.jQuery) {
        jQuery.ajaxPrefilter((options, original, xhr) => {
            const url = new URL(options.url, window.location.href);
            if (url.origin === window.location.origin && !/^(GET|HEAD|OPTIONS|TRACE)$/i.test(options.type)) {
                const token = document.querySelector('meta[name="request-verification-token"]');
                if (token) xhr.setRequestHeader('RequestVerificationToken', token.content);
            }
        });
    }
})();

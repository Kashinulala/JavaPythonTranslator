document.addEventListener('DOMContentLoaded', function() {
    // Инициализация CodeMirror для ввода (Java)
    const codeInputEditor = CodeMirror.fromTextArea(document.getElementById('code-input'), {
        lineNumbers: true,
        mode: 'text/x-java',
        theme: 'dracula',
        lineWrapping: true,
        indentUnit: 4,
        matchBrackets: true,
        autoCloseBrackets: true,
        viewportMargin: Infinity
    });
    
    // Инициализация CodeMirror для вывода (Python)
    const codeOutputEditor = CodeMirror.fromTextArea(document.getElementById('code-output'), {
        lineNumbers: true,
        mode: 'python',
        theme: 'dracula',
        lineWrapping: true,
        indentUnit: 4,
        readOnly: true, // Только для чтения
        matchBrackets: true,
        viewportMargin: Infinity
    });
    
    // Обновим ссылки на элементы
    const submitButton = document.querySelector('#Translate');
    const copyButton = document.querySelector('#Copy');
    
    // Базовый URL API
    const API_URL = 'https://localhost:7240/api/Translator';
    
    // Обработка отправки кода
    submitButton.addEventListener('click', async function(e) {
        e.preventDefault();

        const code = codeInputEditor.getValue().trim();
        if (!code) {
            showMessage('Введите код для трансляции', 'warning');
            return;
        }

        try {
            // Показать индикатор загрузки
            submitButton.disabled = true;
            submitButton.textContent = 'Обработка...';

            // Отправка запроса на бэкенд
            const response = await fetch(`${API_URL}/translate`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ javaCode: code })
            });

            let result;
            if (!response.ok) {
                // Сервер вернул ошибку (4xx, 5xx)
                try {
                    result = await response.json(); // Предполагаем, что сервер всегда возвращает JSON
                } catch (jsonError) {
                    // Если тело ответа не JSON (редко, но возможно), создаём объект ошибки
                    console.warn('Response body is not JSON:', jsonError);
                    result = {
                        success: false,
                        message: `HTTP ${response.status}: ${response.statusText}`,
                        errors: [{ line: 0, column: 0, message: `HTTP ${response.status}: ${response.statusText}` }]
                    };
                }
                // Не вызываем throw, обрабатываем результат дальше
            } else {
                // Успешный ответ (2xx)
                result = await response.json();
            }

            // Заполняем поле вывода на основе результата (успешного или с ошибками от API)
            if (result.success) {
                codeOutputEditor.setValue(result.translatedCode); 
                showMessage(result.message || 'Код успешно транслирован', 'success');
            } else {
                // Результат содержит ошибки из API
                showMessage(result.message || 'Произошла ошибка при анализе', 'error');
                // Показываем детали ошибок в редакторе вывода
                let errorMessage = '';
                if (result.errors && result.errors.length > 0) {
                    errorMessage = result.errors.map(err => `Line ${err.line}, Column ${err.column}: ${err.message}`).join('\n');
                } else {
                    errorMessage = result.message || 'Неизвестная ошибка';
                }
                codeOutputEditor.setValue(errorMessage);
            }

        } catch (error) {
            // Сюда попадают только **сетевые ошибки** или **ошибки парсинга JSON**, если response.ok был true
            console.error('Сетевая ошибка или ошибка парсинга JSON:', error);
            codeOutputEditor.setValue(`Ошибка подключения: ${error.message}`);
            showMessage('Ошибка соединения с сервером', 'error');
        } finally {
            // Восстановить кнопку
            submitButton.disabled = false;
            submitButton.textContent = 'Отправить';
        }
    });
    
    // Копирование результата в буфер обмена
    copyButton.addEventListener('click', function() {
        const outputText = codeOutputEditor.getValue();
        if (!outputText) {
            return;
        }
        
        navigator.clipboard.writeText(outputText)
            .then(() => {
                showMessage('Код скопирован в буфер обмена', 'success');
                // Визуальный feedback
                copyButton.textContent = 'Скопировано!';
                setTimeout(() => {
                    copyButton.textContent = 'Скопировать';
                }, 2000);
            })
            .catch(err => {
                console.error('Ошибка копирования:', err);
                showMessage('Не удалось скопировать', 'error');
            });
    });
    
    // Вспомогательная функция для уведомлений
    function showMessage(message, type) {
        const notification = document.createElement('div');
        notification.className = `notification ${type}`;
        notification.textContent = message;
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 10px 20px;
            background: ${type === 'success' ? '#2ecc71' : 
                         type === 'error' ? '#e74c3c' : 
                         type === 'warning' ? '#f39c12' : '#3498db'};
            color: white;
            border-radius: 4px;
            z-index: 1000;
        `;
        
        document.body.appendChild(notification);
        setTimeout(() => notification.remove(), 3000);
    }
    
    // Адаптация высоты редакторов при изменении размера окна
    window.addEventListener('resize', function() {
        setTimeout(() => {
            codeInputEditor.refresh();
            codeOutputEditor.refresh();
        }, 100);
    });
    
    // Инициализируем высоту редакторов после загрузки
    setTimeout(() => {
        codeInputEditor.refresh();
        codeOutputEditor.refresh();
    }, 300);
});

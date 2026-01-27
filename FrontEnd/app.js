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
    const API_URL = 'http://localhost:5000/api';
    
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
                body: JSON.stringify({ code: code })
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const result = await response.json();
            
            // Заполняем поле вывода
            if (result.success) {
                codeOutputEditor.setValue(result.translatedCode || result.output || '');
                showMessage('Код успешно трансформирован', 'success');
                
                // Обновляем отображение
                setTimeout(() => {
                    codeOutputEditor.refresh();
                }, 100);
            } else {
                codeOutputEditor.setValue(result.error || 'Ошибка трансляции');
                showMessage(result.error || 'Ошибка при трансляции', 'error');
            }
            
        } catch (error) {
            console.error('Ошибка:', error);
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


// document.addEventListener('DOMContentLoaded', function() {
//     const codeInput = document.getElementById('code-input');
//     const codeOutput = document.getElementById('code-output');
//     const submitButton = document.querySelector('#Translate');
//     const copyButton = document.querySelector('#Copy');
    
//     // Базовый URL API
//     const API_URL = 'http://localhost:5000/api';
    
//     // Обработка отправки кода
//     submitButton.addEventListener('click', async function(e) {
//         e.preventDefault();
        
//         const code = codeInput.value.trim();
//         if (!code) {
//             showMessage('Введите код для трансляции', 'warning');
//             return;
//         }
        
//         try {
//             // Показать индикатор загрузки
//             submitButton.disabled = true;
//             submitButton.textContent = 'Обработка...';
            
//             // Отправка запроса на бэкенд
//             const response = await fetch(`${API_URL}/translate`, {
//                 method: 'POST',
//                 headers: {
//                     'Content-Type': 'application/json',
//                 },
//                 body: JSON.stringify({ code: code })
//             });
            
//             if (!response.ok) {
//                 throw new Error(`HTTP ${response.status}: ${response.statusText}`);
//             }
            
//             const result = await response.json();
            
//             // Заполняем поле вывода
//             if (result.success) {
//                 codeOutput.value = result.translatedCode || result.output;
//                 showMessage('Код успешно трансформирован', 'success');
//             } else {
//                 codeOutput.value = result.error || 'Ошибка трансляции';
//                 showMessage(result.error || 'Ошибка при трансляции', 'error');
//             }
            
//         } catch (error) {
//             console.error('Ошибка:', error);
//             codeOutput.value = `Ошибка подключения: ${error.message}`;
//             showMessage('Ошибка соединения с сервером', 'error');
//         } finally {
//             // Восстановить кнопку
//             submitButton.disabled = false;
//             submitButton.textContent = 'Отправить';
//         }
//     });
    
//     // Копирование результата в буфер обмена
//     copyButton.addEventListener('click', function() {
//         if (!codeOutput.value) {
//             return;
//         }
        
//         navigator.clipboard.writeText(codeOutput.value)
//             .then(() => {
//                 showMessage('Код скопирован в буфер обмена', 'success');
//                 // Визуальный feedback
//                 copyButton.textContent = 'Скопировано!';
//                 setTimeout(() => {
//                     copyButton.textContent = 'Скопировать';
//                 }, 2000);
//             })
//             .catch(err => {
//                 console.error('Ошибка копирования:', err);
//                 showMessage('Не удалось скопировать', 'error');
//             });
//     });
    
//     // Вспомогательная функция для уведомлений
//     function showMessage(message, type) {
//         // Можно использовать toast-уведомления или alert для простоты
//         alert(`[${type.toUpperCase()}] ${message}`);
        
//         // Или создать кастомное уведомление:
//         const notification = document.createElement('div');
//         notification.className = `notification ${type}`;
//         notification.textContent = message;
//         notification.style.cssText = `
//             position: fixed;
//             top: 20px;
//             right: 20px;
//             padding: 10px 20px;
//             background: ${type === 'success' ? '#2ecc71' : 
//                          type === 'error' ? '#e74c3c' : 
//                          type === 'warning' ? '#f39c12' : '#3498db'};
//             color: white;
//             border-radius: 4px;
//             z-index: 1000;
//         `;
        
//         document.body.appendChild(notification);
//         setTimeout(() => notification.remove(), 3000);
//     }
// });
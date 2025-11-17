
class SessionTimeoutManager {
    constructor() {
        this.config = {
            timeoutMinutes: 10,
            warningMinutes: 2,
            checkIntervalSeconds: 30
        };

        this.warningShown = false;
        this.checkInterval = null;
        this.heartbeatInterval = null;
        this.countdownInterval = null;
        this.lastActivityTime = new Date();

        this.init();
    }

    async init() {
        try {
            // Get timeout configuration from server
            const response = await fetch('/Account/GetTimeoutConfig');
            if (response.ok) {
                this.config = await response.json();
            }
        } catch (error) {
            console.warn('Could not load timeout configuration, using defaults');
        }

        this.setupActivityListeners();
        this.startSessionCheck();
        this.startHeartbeat();
    }

    setupActivityListeners() {
        // Track user activity
        const events = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];
        const throttledActivityHandler = this.throttle(() => {
            this.lastActivityTime = new Date();
            this.hideWarning();
        }, 1000);

        events.forEach(event => {
            document.addEventListener(event, throttledActivityHandler, true);
        });
    }

    startSessionCheck() {
        this.checkInterval = setInterval(() => {
            this.checkSession();
        }, this.config.checkIntervalSeconds * 1000);
    }

    startHeartbeat() {
        // Send heartbeat every 2 minutes to keep session active
        this.heartbeatInterval = setInterval(() => {
            this.sendHeartbeat();
        }, 120000); // 2 minutes
    }

    async checkSession() {
        try {
            const response = await fetch('@Url.Action("check", "session")');

            if (response.status === 401) {
                this.handleSessionExpired();
                return;
            }

            if (response.ok) {
                const data = await response.json();

                if (!data.isValid) {
                    this.handleSessionExpired();
                    return;
                }

                // Show warning if remaining time is less than warning threshold
                if (data.remainingMinutes <= this.config.warningMinutes && !this.warningShown) {
                    this.showWarning(Math.ceil(data.remainingMinutes));
                }
            }
        } catch (error) {
            console.error('Session check failed:', error);
        }
    }

    async sendHeartbeat() {
        try {
            const response = await fetch('/session/heartbeat', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.status === 401) {
                this.handleSessionExpired();
            }
        } catch (error) {
            console.error('Heartbeat failed:', error);
        }
    }

    showWarning(remainingMinutes) {
        this.warningShown = true;

        // Create warning modal
        const modal = document.createElement('div');
        modal.id = 'session-timeout-warning';
        modal.className = 'session-timeout-modal';
        modal.innerHTML = `
            <div class="session-timeout-content">
                <h3>Session Timeout Warning</h3>
                <p>Your session will expire in <span id="countdown-timer">${remainingMinutes}</span> minute(s) due to inactivity.</p>
                <p>Click "Stay Logged In" to continue your session.</p>
                <div class="session-timeout-buttons">
                    <button id="stay-logged-in" class="btn btn-primary">Stay Logged In</button>
                    <button id="logout-now" class="btn btn-secondary">Logout Now</button>
                </div>
            </div>
        `;

        document.body.appendChild(modal);

        // Add event listeners
        document.getElementById('stay-logged-in').addEventListener('click', () => {
            this.extendSession();
        });

        document.getElementById('logout-now').addEventListener('click', () => {
            this.logout();
        });

        // Start countdown
        this.startCountdown(remainingMinutes);
    }

    startCountdown(minutes) {
        let remainingSeconds = minutes * 60;
        const timerElement = document.getElementById('countdown-timer');

        this.countdownInterval = setInterval(() => {
            remainingSeconds--;

            if (remainingSeconds <= 0) {
                this.handleSessionExpired();
                return;
            }

            const mins = Math.floor(remainingSeconds / 60);
            const secs = remainingSeconds % 60;

            if (timerElement) {
                timerElement.textContent = `${mins}:${secs.toString().padStart(2, '0')}`;
            }
        }, 1000);
    }

    hideWarning() {
        this.warningShown = false;
        const modal = document.getElementById('session-timeout-warning');
        if (modal) {
            modal.remove();
        }

        if (this.countdownInterval) {
            clearInterval(this.countdownInterval);
            this.countdownInterval = null;
        }
    }

    async extendSession() {
        await this.sendHeartbeat();
        this.hideWarning();
    }

    async logout() {
        try {
            const response = await fetch(window.app.logout, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.ok) {
                window.location.href = window.app.logoutUrl; //'/Account/LogoutView?reason=UserLogout';
            }
        } catch (error) {
            console.error('Logout failed:', error);
            window.location.href = window.app.logoutUrl; //'/Account/LogoutView?reason=UserLogout';
        }
    }

    handleSessionExpired() {
        this.cleanup();

        // Show session expired message
        alert('Your session has expired. You will be redirected to the login page.');
        window.location.href = window.app.timeoutUrl; //'/Account/LogoutView?reason=SessionTimeout';
    }

    cleanup() {
        if (this.checkInterval) {
            clearInterval(this.checkInterval);
        }

        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
        }

        if (this.countdownInterval) {
            clearInterval(this.countdownInterval);
        }

        this.hideWarning();
    }

    throttle(func, limit) {
        let inThrottle;
        return function () {
            const args = arguments;
            const context = this;
            if (!inThrottle) {
                func.apply(context, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        }
    }
}

// CSS for the warning modal
const style = document.createElement('style');
style.textContent = `
    .session-timeout-modal {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background-color: rgba(0, 0, 0, 0.7);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 10000;
    }

    .session-timeout-content {
        background: white;
        padding: 30px;
        border-radius: 8px;
        box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
        text-align: center;
        max-width: 400px;
        width: 90%;
    }

    .session-timeout-content h3 {
        color: #d9534f;
        margin-bottom: 20px;
    }

    .session-timeout-content p {
        margin-bottom: 15px;
        color: #333;
    }

    .session-timeout-buttons {
        margin-top: 20px;
    }

    .session-timeout-buttons button {
        margin: 0 10px;
        padding: 10px 20px;
        border: none;
        border-radius: 4px;
        cursor: pointer;
        font-size: 14px;
    }

    .btn-primary {
        background-color: #007bff;
        color: white;
    }

    .btn-primary:hover {
        background-color: #0056b3;
    }

    .btn-secondary {
        background-color: #6c757d;
        color: white;
    }

    .btn-secondary:hover {
        background-color: #545b62;
    }

    #countdown-timer {
        font-weight: bold;
        color: #d9534f;
        font-size: 18px;
    }
`;
document.head.appendChild(style);

// Initialize session timeout manager when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    // Only initialize if user is authenticated
    if (document.body.getAttribute('data-authenticated') === 'true') {
        window.sessionTimeoutManager = new SessionTimeoutManager();
    }
});

// Handle page visibility change
document.addEventListener('visibilitychange', () => {
    if (window.sessionTimeoutManager) {
        if (document.hidden) {
            // Page is hidden, reduce check frequency
            window.sessionTimeoutManager.cleanup();
        } else {
            // Page is visible again, resume normal operation
            window.sessionTimeoutManager.init();
        }
    }
});
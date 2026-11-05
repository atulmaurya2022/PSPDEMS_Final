class SessionTimeoutManager {
    constructor() {
        this.config = {
            timeoutMinutes: 10,
            warningMinutes: 2,
            checkIntervalSeconds: 30
        };

        this.warningShown = false;
        this.isExpired = false;
        this.userInteracted = false;
        this.checkInterval = null;
        this.heartbeatInterval = null;
        this.countdownInterval = null;
        this.lastActivityTime = new Date();

        this.init();
    }

    async init() {
        try {
            // Get timeout configuration from server
            const response = await fetch('/Account/GetTimeoutConfig', {
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
            if (response.ok) {
                this.config = await response.json();
            }
        } catch (error) {
            console.warn('Could not load timeout configuration, using defaults');
        }

        this.setupActivityListeners();
        this.startSessionCheck();
        this.startActivityHeartbeat();
    }

    setupActivityListeners() {
        // Track genuine physical user interactions
        const events = ['mousedown', 'mousemove', 'keypress', 'keydown', 'scroll', 'touchstart', 'click'];
        const throttledActivityHandler = this.throttle(() => {
            this.lastActivityTime = new Date();
            this.userInteracted = true;
        }, 1000);

        events.forEach(event => {
            document.addEventListener(event, throttledActivityHandler, true);
        });
    }

    startSessionCheck() {
        if (this.checkInterval) {
            clearInterval(this.checkInterval);
        }
        const intervalMs = (this.config.checkIntervalSeconds || 30) * 1000;
        this.checkInterval = setInterval(() => {
            this.checkSession();
        }, intervalMs);
    }

    startActivityHeartbeat() {
        // Send heartbeat every 2 minutes ONLY IF user has physically interacted with the page
        // and the warning modal is not currently displayed.
        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
        }
        this.heartbeatInterval = setInterval(() => {
            if (this.userInteracted && !this.warningShown && !this.isExpired) {
                this.sendHeartbeat();
                this.userInteracted = false;
            }
        }, 120000); // 2 minutes
    }

    async checkSession() {
        if (this.isExpired) return;

        try {
            const response = await fetch('/session/check', {
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            // If unauthorized or redirected to login, session has expired
            if (response.status === 401 || response.redirected) {
                this.handleSessionExpired();
                return;
            }

            if (response.ok) {
                const data = await response.json();

                if (!data || !data.isValid) {
                    this.handleSessionExpired();
                    return;
                }

                // Show warning if remaining time is less than or equal to warning threshold
                const warningMinutes = this.config.warningMinutes || data.warningThreshold || 2;
                if (data.remainingMinutes <= warningMinutes && !this.warningShown) {
                    this.showWarning(Math.max(1, Math.ceil(data.remainingMinutes)));
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
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.status === 401 || response.redirected) {
                this.handleSessionExpired();
            }
        } catch (error) {
            console.error('Heartbeat failed:', error);
        }
    }

    showWarning(remainingMinutes) {
        if (this.warningShown || this.isExpired) return;
        this.warningShown = true;

        // Remove any previous modal if exists
        const existing = document.getElementById('session-timeout-warning');
        if (existing) existing.remove();

        // Create warning modal
        const modal = document.createElement('div');
        modal.id = 'session-timeout-warning';
        modal.className = 'session-timeout-modal';
        modal.innerHTML = `
            <div class="session-timeout-content">
                <h3>Session Timeout Warning</h3>
                <p>Your session will expire in <span id="countdown-timer">${remainingMinutes}:00</span> due to inactivity.</p>
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

        if (this.countdownInterval) {
            clearInterval(this.countdownInterval);
        }

        this.countdownInterval = setInterval(() => {
            remainingSeconds--;

            if (remainingSeconds <= 0) {
                clearInterval(this.countdownInterval);
                this.countdownInterval = null;
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
        this.userInteracted = true;
        this.hideWarning();
    }

    async logout() {
        this.cleanup();
        try {
            await fetch('/Account/Logout', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
        } catch (error) {
            console.error('Logout failed:', error);
        } finally {
            const logoutUrl = (window.app && window.app.logoutUrl) ? window.app.logoutUrl : '/Account/LogoutView?reason=UserLogout';
            window.location.href = logoutUrl;
        }
    }

    handleSessionExpired() {
        if (this.isExpired) return;
        this.isExpired = true;

        this.cleanup();

        // Show session expired message
        alert('Your session has expired. You will be redirected to the login page.');
        const timeoutUrl = (window.app && window.app.timeoutUrl) ? window.app.timeoutUrl : '/Account/LogoutView?reason=SessionTimeout';
        window.location.href = timeoutUrl;
    }

    cleanup() {
        if (this.checkInterval) {
            clearInterval(this.checkInterval);
            this.checkInterval = null;
        }

        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
            this.heartbeatInterval = null;
        }

        if (this.countdownInterval) {
            clearInterval(this.countdownInterval);
            this.countdownInterval = null;
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
    if (window.sessionTimeoutManager && !document.hidden) {
        // Page became visible again, immediately check session status
        window.sessionTimeoutManager.checkSession();
    }
});

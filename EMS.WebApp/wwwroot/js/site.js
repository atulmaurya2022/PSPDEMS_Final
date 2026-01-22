/**
 * Bootstrap 5 alert
 * @param {string} type    – one of 'success', 'danger', 'warning', 'info'
 * @param {string} message – the alert text
 */
window.showAlert = function showAlert(type, message) {
    const wrapper = document.createElement('div');
    wrapper.innerHTML = `
    <div class="alert alert-${type} alert-dismissible fade show" role="alert">
      ${message}
      <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    </div>`;
    document.getElementById('alertPlaceholder').append(wrapper);

    // Optional: auto-dismiss after 5 seconds
    setTimeout(() => {
        const alert = bootstrap.Alert.getOrCreateInstance(wrapper.querySelector('.alert'));
        alert.close();
    }, 5000);
};



document.addEventListener('DOMContentLoaded', function () {
    const dropdowns = document.querySelectorAll('.top-nav .dropdown');

    dropdowns.forEach(dropdown => {
        const toggle = dropdown.querySelector('.dropdown-toggle');
        const menu = dropdown.querySelector('.dropdown-menu.mega-menu');

        if (toggle && menu) {
            // Remove Bootstrap default behavior
            toggle.removeAttribute('data-bs-toggle');

            // Count menu items and set data attribute
            const setItemCount = () => {
                const items = menu.querySelectorAll('.menu-tile');
                const count = items.length;
                menu.setAttribute('data-item-count', count);

                // Add class for many items
                if (count > 20) {
                    menu.classList.add('many-items');
                } else {
                    menu.classList.remove('many-items');
                }
            };

            // Initialize item count
            setItemCount();

            // Click handler
            toggle.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                // Close other dropdowns
                dropdowns.forEach(otherDropdown => {
                    if (otherDropdown !== dropdown) {
                        otherDropdown.classList.remove('show');
                    }
                });

                // Toggle current dropdown
                dropdown.classList.toggle('show');

                // Update item count when opening
                if (dropdown.classList.contains('show')) {
                    setItemCount();
                }
            });
        }
    });

    // Close on outside click
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.dropdown')) {
            dropdowns.forEach(dropdown => {
                dropdown.classList.remove('show');
            });
        }
    });

    // Prevent closing when clicking inside menu
    document.querySelectorAll('.dropdown-menu.mega-menu').forEach(menu => {
        menu.addEventListener('click', function (e) {
            e.stopPropagation();
        });
    });

    // Close after clicking menu item
    document.querySelectorAll('.mega-menu .menu-tile').forEach(link => {
        link.addEventListener('click', function () {
            dropdowns.forEach(dropdown => {
                dropdown.classList.remove('show');
            });
        });
    });
});

document.addEventListener('DOMContentLoaded', function () {
    // Check if there's an active menu item and highlight parent dropdown
    const activeMenuItem = document.querySelector('.mega-menu .menu-tile.active');
    if (activeMenuItem) {
        const parentDropdown = activeMenuItem.closest('.dropdown');
        if (parentDropdown) {
            parentDropdown.classList.add('has-active-item');
        }
    }
});


document.addEventListener('DOMContentLoaded', function () {
    const userData = {
        name: '@userName', // Use existing Razor variable
        employeeId: 'EMP',
        designation: 'Senior ',
        plant: 'Plant'
    };


    fetchUserData().then(userData => {
        updateUserInfo(userData);
    });

    function updateUserInfo(data) {
        // Update user initial
        const initial = data.fullName ? data.fullName.charAt(0).toUpperCase() : 'U';
        document.getElementById('userInitial').textContent = initial;

        // Determine display role based on plant
        let displayRole = data.roleName || '';
        const plantName = data.plantName || '';
        const isTribeniPlant = plantName.toLowerCase() === 'tribeni';

        // Replace Compounder with Pharmacist for Tribeni plant
        if (isTribeniPlant && displayRole.toLowerCase().includes('compounder')) {
            displayRole = displayRole.replace(/compounder/gi, 'Pharmacist');
        }

        document.getElementById('userName').textContent = data.fullName || '';
        document.getElementById('userEmpId').textContent = data.adid || '';
        document.getElementById('userDesignation').textContent = displayRole;
        document.getElementById('userPlant').textContent = data.plantName || '';

    }

    updateUserInfo(userData);


});

// Function to fetch user data from backend (example)
async function fetchUserData() {
    try {

        const response = await fetch(window.app.meUrl, { credentials: 'same-origin' });
        if (!response.ok) throw new Error('HTTP ' + response.status);

        const data = await response.json();

        // Log the requested fields
        //console.log('User info:', {
        //    fullName: data.fullName || '',
        //    adid: data.adid || '',
        //    roleName: data.roleName || '',
        //    plantName: data.plantName || ''
        //});

        return data;
    } catch (error) {
        console.error('Error fetching user data:', error);

        //const fallback = {
        //    fullName: '',
        //    adid: 'AD000',
        //    roleName: 'User',
        //    plantName: 'Plant'
        //};
        //console.log('User info (fallback):', fallback);
        return fallback;
    }
}
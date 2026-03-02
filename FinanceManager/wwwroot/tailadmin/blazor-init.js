(function () {
  function dispatch(eventName) {
    try {
      document.dispatchEvent(new Event(eventName));
    } catch (_e) {
      var evt = document.createEvent('Event');
      evt.initEvent(eventName, true, true);
      document.dispatchEvent(evt);
    }
  }

  // Intentionally do not capture/replay Tailadmin DOMContentLoaded chart/map initializers.
  // Replaying those routines can conflict with Blazor-managed ApexChart components.

  function initFlatpickr() {
    if (!window.flatpickr) return;

    try {
      window.flatpickr('.datepicker', {
        mode: 'range',
        static: true,
        monthSelectorType: 'static',
        dateFormat: 'M j, Y',
        defaultDate: [new Date().setDate(new Date().getDate() - 6), new Date()],
        prevArrow:
          '<svg class="stroke-current" width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M15.25 6L9 12.25L15.25 18.5" stroke="" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        nextArrow:
          '<svg class="stroke-current" width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M8.75 19L15 12.75L8.75 6.5" stroke="" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        onReady: function (_selectedDates, dateStr, instance) {
          instance.element.value = dateStr.replace('to', '-');
          var customClass = instance.element.getAttribute('data-class');
          if (customClass) instance.calendarContainer.classList.add(customClass);
        },
        onChange: function (_selectedDates, dateStr, instance) {
          instance.element.value = dateStr.replace('to', '-');
        },
      });
    } catch (_e) {
      // ignore
    }

    try {
      window.flatpickr('.datepickerTwo', {
        static: true,
        monthSelectorType: 'static',
        dateFormat: 'M j, Y',
        prevArrow:
          '<svg class="stroke-current" width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M15.25 6L9 12.25L15.25 18.5" stroke="" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        nextArrow:
          '<svg class="stroke-current" width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M8.75 19L15 12.75L8.75 6.5" stroke="" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        onReady: function (_selectedDates, dateStr, instance) {
          instance.element.value = dateStr.replace('to', '-');
          var customClass = instance.element.getAttribute('data-class');
          if (customClass) instance.calendarContainer.classList.add(customClass);
        },
        onChange: function (_selectedDates, dateStr, instance) {
          instance.element.value = dateStr.replace('to', '-');
        },
      });
    } catch (_e) {
      // ignore
    }
  }

  function updateYear() {
    try {
      var year = document.getElementById('year');
      if (year) year.textContent = new Date().getFullYear();
    } catch (_e) {
      // ignore
    }
  }

  function refreshWidgets() {
    // Wait until after Blazor has painted the new page DOM.
    try {
      requestAnimationFrame(function () {
        initFlatpickr();
        updateYear();

        try {
          window.dispatchEvent(new Event('resize'));
        } catch (_e) {
          // ignore
        }
      });
    } catch (_e) {
      // ignore
    }
  }

  window.tailadminBlazor = window.tailadminBlazor || {};
  window.tailadminBlazor.refresh = function () {
    dispatch('tailadmin:refresh');
    refreshWidgets();
  };

  document.addEventListener('tailadmin:refresh', function () {
    refreshWidgets();
  });
})();

/* ==========================================================================
   FASHION STORE - User Behavior Tracker Script
   Theo dõi Session, Dwell Time, Rage Clicks, Phễu chuyển đổi & Gợi ý AI
   ========================================================================== */

(function () {
  'use strict';

  // 1. DUY TRÌ SESSION ID CHO KHÁCH VÃNG LAI & NGƯỜI DÙNG
  function getOrCreateSessionId() {
    let sid = sessionStorage.getItem('fs_behavior_sid');
    if (!sid) {
      const randStr = Math.random().toString(36).substring(2, 11);
      sid = `sid_${randStr}_${Date.now()}`;
      sessionStorage.setItem('fs_behavior_sid', sid);
    }
    return sid;
  }

  const SESSION_ID = getOrCreateSessionId();

  // 2. PHÂN LOẠI THIẾT BỊ
  function detectDeviceType() {
    const ua = navigator.userAgent;
    if (/(tablet|ipad|playbook|silk)|(android(?!.*mobi))/i.test(ua)) {
      return 'Tablet';
    }
    if (/Mobile|iP(hone|od)|Android|BlackBerry|IEMobile|Kindle|Silk-Accelerated|(hpw|web)OS|Opera M(obi|ini)/i.test(ua) || window.innerWidth < 768) {
      return 'Mobile';
    }
    return 'Desktop';
  }

  const DEVICE_TYPE = detectDeviceType();

  // 3. API GỬI BEACON / FETCH VỀ SERVER
  function sendLog(actionType, data = {}) {
    const payload = {
      sessionId: SESSION_ID,
      deviceType: DEVICE_TYPE,
      pageUrl: window.location.pathname + window.location.search,
      actionType: actionType,
      productId: data.productId || getProductIdFromPage(),
      searchQuery: data.searchQuery || null,
      dwellTimeSeconds: data.dwellTimeSeconds || 0,
      isRageClick: data.isRageClick || false,
      recommendationSource: data.recommendationSource || null,
      recommendationBlockId: data.recommendationBlockId || null
    };

    const blob = new Blob([JSON.stringify(payload)], { type: 'application/json' });
    if (navigator.sendBeacon) {
      navigator.sendBeacon('/BehaviorTracker/Log', blob);
    } else {
      fetch('/BehaviorTracker/Log', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
        keepalive: true
      }).catch(function () {});
    }
  }

  function getProductIdFromPage() {
    const el = document.querySelector('[data-product-id]');
    if (el) {
      const id = parseInt(el.getAttribute('data-product-id'), 10);
      if (!isNaN(id) && id > 0) return id;
    }
    return null;
  }

  // 4. THEO DÕI PAGEVIEW VÀ HÀNH TRÌNH MUA SẮM
  const currentPath = window.location.pathname.toLowerCase();

  // Determine initial action type based on page path
  let pageActionType = 'View';
  if (currentPath.includes('/order/checkout')) {
    pageActionType = 'CheckoutView';
  } else if (currentPath.includes('/order/orderconfirmation') || currentPath.includes('/order/success')) {
    pageActionType = 'Purchase';
  }

  // Send initial page view log
  sendLog(pageActionType);

  // 5. THEO DÕI THỜI GIAN TƯƠNG TÁC THỰC TẾ (DWELL TIME)
  let activeStartTime = Date.now();
  let accumulatedDwellTime = 0;
  let isActive = true;

  function updateDwellTime() {
    if (isActive) {
      const now = Date.now();
      accumulatedDwellTime += (now - activeStartTime) / 1000;
      activeStartTime = now;
    }
  }

  document.addEventListener('visibilitychange', function () {
    if (document.visibilityState === 'hidden') {
      updateDwellTime();
      isActive = false;
      if (accumulatedDwellTime > 1) {
        sendLog('View', { dwellTimeSeconds: Math.round(accumulatedDwellTime) });
      }
    } else {
      activeStartTime = Date.now();
      isActive = true;
    }
  });

  window.addEventListener('beforeunload', function () {
    updateDwellTime();
    if (accumulatedDwellTime > 1) {
      sendLog('View', { dwellTimeSeconds: Math.round(accumulatedDwellTime) });
    }
  });

  // 6. PHÁT HIỆN CÚ NHẤP BỰC BỘI (RAGE CLICK DETECTION: >=3 CLICKS TRONG 500MS)
  let clickHistory = [];

  document.addEventListener('click', function (e) {
    const now = Date.now();
    const clickPos = { x: e.clientX, y: e.clientY, time: now };

    // Filter out clicks older than 500ms
    clickHistory = clickHistory.filter(c => now - c.time <= 500);
    clickHistory.push(clickPos);

    if (clickHistory.length >= 3) {
      // Check if all clicks in history are within a 50px bounding box
      const minX = Math.min(...clickHistory.map(c => c.x));
      const maxX = Math.max(...clickHistory.map(c => c.x));
      const minY = Math.min(...clickHistory.map(c => c.y));
      const maxY = Math.max(...clickHistory.map(c => c.y));

      if ((maxX - minX <= 50) && (maxY - minY <= 50)) {
        sendLog('RageClick', { isRageClick: true });
        clickHistory = []; // Reset after firing
      }
    }

    // Capture clicks on Recommendation Product Cards
    const recCard = e.target.closest('[data-rec-source]');
    if (recCard) {
      const recSource = recCard.getAttribute('data-rec-source');
      const recBlock = recCard.getAttribute('data-rec-block') || 'recommendation_section';
      const pid = parseInt(recCard.getAttribute('data-product-id'), 10) || null;
      sendLog('Click', { productId: pid, recommendationSource: recSource, recommendationBlockId: recBlock });
    }
  });

  // 7. THEO DÕI TÌM KIẾM (SEARCH TRACKING)
  let searchTimeout = null;
  document.addEventListener('input', function (e) {
    if (e.target && (e.target.id === 'liveSearchInput' || e.target.name === 'searchQuery')) {
      const q = e.target.value.trim();
      if (q.length >= 2) {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(function () {
          sendLog('Search', { searchQuery: q });
        }, 800);
      }
    }
  });

  // Export helper function to track add to cart globally
  window.FS_TrackAddToCart = function (productId) {
    sendLog('AddToCart', { productId: productId });
  };

})();

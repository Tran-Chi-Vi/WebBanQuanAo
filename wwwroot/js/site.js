/* ==========================================================================
   WEBBANQUANAO - Main Interactive JS v2.0
   Features: Scroll Animations, Live Search, Flying Cart, Navbar Effects
   ========================================================================== */

/* =========================================================================
   STORE FRONT LIGHT / DARK THEME TOGGLE SYSTEM
   ========================================================================= */
function applyUserThemeUI(isDark) {
  const icon = document.getElementById('userThemeIcon');
  if (isDark) {
    document.body.classList.add('user-dark-mode');
    document.documentElement.classList.add('user-dark-mode');
    document.documentElement.style.backgroundColor = '#0b0f19';
    document.body.style.backgroundColor = '#0b0f19';
    document.documentElement.style.colorScheme = 'dark';
    if (icon) {
      icon.className = 'bi bi-sun-fill fs-5 text-warning';
    }
  } else {
    document.body.classList.remove('user-dark-mode');
    document.documentElement.classList.remove('user-dark-mode');
    document.documentElement.style.backgroundColor = '';
    document.body.style.backgroundColor = '';
    document.documentElement.style.colorScheme = 'light';
    if (icon) {
      icon.className = 'bi bi-moon-stars-fill fs-5 text-indigo';
      icon.style.color = '#6366f1';
    }
  }
}

function toggleUserStoreTheme() {
  const isDark = document.body.classList.contains('user-dark-mode') || document.documentElement.classList.contains('user-dark-mode');
  const nextMode = isDark ? 'light' : 'dark';
  localStorage.setItem('fs_user_theme', nextMode);
  localStorage.setItem('fs_admin_theme', nextMode);
  applyUserThemeUI(!isDark);
}

/* =========================================================================
   GLOBAL SHOW/HIDE PASSWORD TOGGLE SYSTEM
   ========================================================================= */
function togglePasswordVisibility(inputId, btnEl) {
  const input = typeof inputId === 'string' ? document.getElementById(inputId) : inputId;
  if (!input) return;
  const icon = (btnEl && btnEl.querySelector) ? (btnEl.querySelector('i') || btnEl) : btnEl;
  if (input.type === 'password') {
    input.type = 'text';
    if (icon && icon.classList) {
      icon.classList.remove('bi-eye');
      icon.classList.add('bi-eye-slash');
    }
  } else {
    input.type = 'password';
    if (icon && icon.classList) {
      icon.classList.remove('bi-eye-slash');
      icon.classList.add('bi-eye');
    }
  }
}

document.addEventListener('DOMContentLoaded', function () {
  const savedTheme = localStorage.getItem('fs_user_theme');
  applyUserThemeUI(savedTheme === 'dark');

  // Auto-wrap any standalone password inputs with eye toggle buttons
  document.querySelectorAll('input[type="password"]').forEach(function(input) {
    if (!input.closest('.input-group')) {
      const parent = input.parentElement;
      if (parent) {
        const wrapper = document.createElement('div');
        wrapper.className = 'input-group';
        input.classList.remove('rounded-3');
        input.classList.add('rounded-start-3');
        parent.insertBefore(wrapper, input);
        wrapper.appendChild(input);
        const btn = document.createElement('button');
        btn.className = 'btn btn-outline-secondary rounded-end-3 px-3';
        btn.type = 'button';
        btn.title = 'Hiện/Ẩn mật khẩu';
        btn.innerHTML = '<i class="bi bi-eye"></i>';
        btn.onclick = function() {
          togglePasswordVisibility(input, btn);
        };
        wrapper.appendChild(btn);
      }
    }
  });

  console.log('WEBBANQUANAO Fashion Engine v2.0 Initialized.');

  // =========================================================================
  // 1. NAVBAR SCROLL EFFECT
  // =========================================================================
  const mainNavbar = document.getElementById('mainNavbar');
  if (mainNavbar) {
    let lastScroll = 0;
    window.addEventListener('scroll', () => {
      const current = window.scrollY;
      if (current > 60) {
        mainNavbar.classList.add('scrolled');
      } else {
        mainNavbar.classList.remove('scrolled');
      }
      lastScroll = current;
    }, { passive: true });
  }

  // =========================================================================
  // 2. SCROLL REVEAL ANIMATIONS
  // =========================================================================
  const revealObserver = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        // Animate in
        entry.target.classList.add('revealed');
      } else {
        // Reset when out of view so it re-animates on scroll back
        entry.target.classList.remove('revealed');
      }
    });
  }, {
    threshold: 0.12,
    rootMargin: '0px 0px -40px 0px'
  });

  document.querySelectorAll('.reveal, .reveal-left, .reveal-right, .reveal-scale').forEach(el => {
    revealObserver.observe(el);
  });

  // =========================================================================
  // 3. INSTANT LIVE SEARCH
  // =========================================================================
  const searchInput = document.getElementById('liveSearchInput');
  const resultsBox = document.getElementById('liveSearchResults');

  if (searchInput && resultsBox) {
    let searchTimeout = null;

    searchInput.addEventListener('input', function () {
      const val = this.value.trim();
      clearTimeout(searchTimeout);
      if (val.length === 0) {
        resultsBox.style.display = 'none';
        resultsBox.innerHTML = '';
        return;
      }

      searchTimeout = setTimeout(() => {
        fetch(`/Product/LiveSearch?query=${encodeURIComponent(val)}`)
          .then(res => res.json())
          .then(data => {
            if (data && data.length > 0) {
              let html = `
                <div class="px-2 py-1 text-muted fw-bold border-bottom pb-2 mb-1"
                     style="font-size:0.72rem;text-transform:uppercase;letter-spacing:0.06em;">
                  🔍 ${data.length} sản phẩm tìm thấy
                </div>
              `;
              data.forEach(item => {
                html += `
                  <a href="/Product/Details/${item.id}" class="d-flex align-items-center gap-3 p-2 text-decoration-none text-dark rounded-3"
                     style="transition:background 0.15s;" onmouseenter="this.style.background='#f8fafc'" onmouseleave="this.style.background='transparent'">
                    <img src="${item.image}" alt="${item.name}" style="width:46px;height:46px;object-fit:cover;border-radius:10px;flex-shrink:0;border:1.5px solid #e2e8f0;" />
                    <div style="min-width:0;flex:1;">
                      <div class="fw-semibold text-truncate" style="font-size:0.88rem;max-width:200px;">${item.name}</div>
                      <small class="text-muted">${item.category}</small>
                    </div>
                    <div class="fw-bold" style="color:#6366f1;font-size:0.88rem;white-space:nowrap;">${item.price}</div>
                  </a>
                `;
              });
              html += `<div class="pt-2 pb-1 border-top mt-1"><a href="/Product?searchQuery=${encodeURIComponent(val)}" class="d-block text-center text-primary small fw-semibold py-1">Xem tất cả kết quả →</a></div>`;
              resultsBox.innerHTML = html;
              resultsBox.style.display = 'block';
            } else {
              resultsBox.innerHTML = `
                <div class="p-3 text-center text-muted small">
                  <i class="bi bi-search d-block fs-3 mb-2 opacity-30"></i>
                  Không tìm thấy sản phẩm phù hợp với "<strong>${val}</strong>"
                </div>`;
              resultsBox.style.display = 'block';
            }
          })
          .catch(err => console.error(err));
      }, 150);
    });

    document.addEventListener('click', function (e) {
      if (!searchInput.contains(e.target) && !resultsBox.contains(e.target)) {
        resultsBox.style.display = 'none';
      }
    });

    searchInput.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') {
        resultsBox.style.display = 'none';
      }
    });
  }

  // =========================================================================
  // 3b. UNIVERSAL INSTANT LIVE SEARCH FOR TABLES & LISTS (Không cần nhấn Enter)
  // =========================================================================
  const allSearchInputs = document.querySelectorAll('input[name="searchQuery"], input[placeholder*="Tìm"], input[placeholder*="search"]');
  allSearchInputs.forEach(input => {
    if (input.id === 'liveSearchInput') return; // Handled separately for dropdown popup

    let debounceTimer = null;
    input.addEventListener('input', function () {
      const query = this.value.trim().toLowerCase();
      const form = this.closest('form');
      const table = document.querySelector('.table');

      // If inside a page with a data table (Admin pages, user lists), filter rows instantly!
      if (table) {
        const rows = table.querySelectorAll('tbody tr');
        rows.forEach(row => {
          const text = row.textContent.toLowerCase();
          row.style.display = text.includes(query) ? '' : 'none';
        });
      } else if (form && window.location.pathname.includes('/Product')) {
        // If inside product catalog page, auto-submit filter form after 350ms typing pause
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => {
          form.submit();
        }, 350);
      }
    });
  });

  // =========================================================================
  // 4. SIGNALR REAL-TIME STOCK (Optional - fallback gracefully)
  // =========================================================================
  if (typeof signalR !== 'undefined') {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/stock")
      .withAutomaticReconnect()
      .build();

    connection.on("StockUpdated", function (variantId, newStockQuantity) {
      const stockBadge = document.querySelector(`.stock-badge-variant-${variantId}`);
      if (stockBadge) {
        stockBadge.textContent = newStockQuantity > 0 ? `Còn ${newStockQuantity} SP` : "Hết hàng";
        stockBadge.className = `stock-badge-variant-${variantId} badge ${newStockQuantity > 0 ? 'bg-success' : 'bg-danger'}`;
      }
    });

    connection.start().catch(err => console.error("SignalR Error:", err));

    window.joinVariantStockGroup = function (variantId) {
      if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke("JoinVariantGroup", parseInt(variantId)).catch(err => console.error(err));
      }
    };
  }

  // =========================================================================
  // 5. TOAST NOTIFICATION
  // =========================================================================
  window.showToast = function (message, icon = 'success') {
    if (typeof Swal !== 'undefined') {
      Swal.mixin({
        toast: true,
        position: 'top-end',
        showConfirmButton: false,
        timer: 3500,
        timerProgressBar: true,
        didOpen: (toast) => {
          toast.addEventListener('mouseenter', Swal.stopTimer);
          toast.addEventListener('mouseleave', Swal.resumeTimer);
        }
      }).fire({ icon, title: message });
    } else {
      alert(message);
    }
  };

  // =========================================================================
  // 6. FLYING CART ANIMATION (Ball flies to cart icon)
  // =========================================================================
  window.animateFlyToCart = function (sourceElement) {
    const cartTarget = document.getElementById('headerCartBtn');
    if (!cartTarget) return;

    let imgSrc = null;
    if (sourceElement) {
      const container = sourceElement.closest('.product-card') || document;
      const img = sourceElement.tagName === 'IMG' ? sourceElement
        : container.querySelector('#mainProductImg')
        || container.querySelector('.product-card-img-wrapper img')
        || container.querySelector('img');
      if (img) imgSrc = img.src;
    }

    const cartRect = cartTarget.getBoundingClientRect();

    const ball = document.createElement('div');
    ball.style.cssText = `
      position: fixed;
      z-index: 999999;
      width: 56px;
      height: 56px;
      border-radius: 50%;
      pointer-events: none;
      overflow: hidden;
      border: 3px solid #6366f1;
      box-shadow: 0 0 0 8px rgba(99,102,241,0.2), 0 8px 24px rgba(99,102,241,0.5);
      will-change: transform, opacity;
      transition: transform 0.75s cubic-bezier(0.18, 0.89, 0.32, 1.1), opacity 0.75s ease;
    `;

    if (imgSrc) {
      const img = document.createElement('img');
      img.src = imgSrc;
      img.style.cssText = 'width:100%;height:100%;object-fit:cover;';
      ball.appendChild(img);
    } else {
      ball.style.background = 'linear-gradient(135deg, #6366f1, #ec4899)';
      ball.innerHTML = '<i class="bi bi-bag-fill" style="color:white;font-size:1.5rem;display:flex;align-items:center;justify-content:center;height:100%;"></i>';
    }

    // Determine start position (center of screen or near source)
    let startX = window.innerWidth / 2 - 28;
    let startY = window.innerHeight / 2 - 28;

    if (sourceElement) {
      const srcRect = sourceElement.getBoundingClientRect();
      startX = srcRect.left + srcRect.width / 2 - 28;
      startY = srcRect.top + srcRect.height / 2 - 28;
    }

    ball.style.left = startX + 'px';
    ball.style.top = startY + 'px';
    document.body.appendChild(ball);

    const endX = cartRect.left + cartRect.width / 2 - 28;
    const endY = cartRect.top + cartRect.height / 2 - 28;

    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        ball.style.transform = `translate(${endX - startX}px, ${endY - startY}px) scale(0.3) rotate(360deg)`;
        ball.style.opacity = '0';
      });
    });

    setTimeout(() => {
      ball.remove();
      // Bounce cart icon
      cartTarget.style.transition = 'transform 0.15s ease';
      cartTarget.style.transform = 'scale(1.35)';
      setTimeout(() => {
        cartTarget.style.transform = 'scale(1)';
      }, 200);
    }, 780);
  };

  // =========================================================================
  // 7. ADD TO CART AJAX
  // =========================================================================
  window.addToCartAjax = function (variantId, quantity = 1, triggerButton = null) {
    fetch('/Cart/AddToCart', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': getCsrfToken()
      },
      body: JSON.stringify({ variantId: parseInt(variantId), quantity: parseInt(quantity) })
    })
    .then(res => res.json())
    .then(data => {
      if (data.success) {
        animateFlyToCart(triggerButton);
        showToast(data.message, 'success');
        updateCartCountBadge(data.cartCount);
      } else {
        showToast(data.message, 'error');
      }
    })
    .catch(err => {
      console.error(err);
      showToast('Có lỗi xảy ra khi thêm vào giỏ hàng.', 'error');
    });
  };

  // =========================================================================
  // 8. TOGGLE FAVORITE
  // =========================================================================
  window.toggleFavoriteAjax = function (productId, buttonElem) {
    fetch(`/Product/ToggleFavorite?productId=${productId}`, {
      method: 'POST',
      headers: { 'RequestVerificationToken': getCsrfToken() }
    })
    .then(res => res.json())
    .then(data => {
      if (data.success) {
        showToast(data.message, data.isFavorite ? 'success' : 'info');
        if (buttonElem) {
          if (data.isFavorite) {
            buttonElem.classList.add('active');
            buttonElem.querySelector('i').className = 'bi bi-heart-fill';
            buttonElem.style.color = '#ec4899';
          } else {
            buttonElem.classList.remove('active');
            buttonElem.querySelector('i').className = 'bi bi-heart';
            buttonElem.style.color = '';
          }
        }
      }
    })
    .catch(err => console.error(err));
  };

  // =========================================================================
  // UTILITIES
  // =========================================================================
  function updateCartCountBadge(count) {
    document.querySelectorAll('.cart-count-badge').forEach(b => {
      b.textContent = count;
      if (count > 0) {
        b.style.setProperty('display', 'flex', 'important');
      } else {
        b.style.setProperty('display', 'none', 'important');
      }
    });
  }

  function getCsrfToken() {
    const t = document.querySelector('input[name="__RequestVerificationToken"]');
    return t ? t.value : '';
  }

  // =========================================================================
  // 9. DEFENSIVE DOUBLE-SUBMIT PROTECTION (Chống click đúp gửi trùng form)
  // =========================================================================
  document.querySelectorAll('form').forEach(form => {
    form.addEventListener('submit', function () {
      const submitBtn = this.querySelector('button[type="submit"]');
      if (submitBtn && !submitBtn.disabled) {
        submitBtn.disabled = true;
        const originalText = submitBtn.innerHTML;
        submitBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Đang xử lý...`;
        setTimeout(() => {
          if (submitBtn.disabled) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalText;
          }
        }, 8000);
      }
    });
  });

  // =========================================================================
  // 10. HERO TYPEWRITER ANIMATION (Hiện ra từng chữ mượt mà, không giật)
  // =========================================================================
  const typewriterEl = document.getElementById('heroTypewriter');
  if (typewriterEl) {
    const phrases = [
      'ĐỊNH GU THỜI TRANG',
      'PHONG CÁCH RIÊNG TƯ',
      'ĐẲNG CẤP TỪNG OUTFIT',
      'HIỆN ĐẠI & TINH TẾ'
    ];
    let pIdx = 0, charIdx = 0, isDeleting = false;

    function typeLoop() {
      const currentPhrase = phrases[pIdx];

      if (isDeleting) {
        const txt = currentPhrase.substring(0, charIdx - 1);
        typewriterEl.textContent = txt || '\u00A0';
        charIdx--;
      } else {
        typewriterEl.textContent = currentPhrase.substring(0, charIdx + 1);
        charIdx++;
      }

      let speed = isDeleting ? 30 : 65;

      if (!isDeleting && charIdx === currentPhrase.length) {
        speed = 2400;
        isDeleting = true;
      } else if (isDeleting && charIdx === 0) {
        isDeleting = false;
        pIdx = (pIdx + 1) % phrases.length;
        speed = 400;
      }

      setTimeout(typeLoop, speed);
    }

    typeLoop();
  }

  // =========================================================================
  // 11. OVERDRIVE: HERO INTERACTIVE PARTICLE CANVAS
  // =========================================================================
  const heroCanvas = document.getElementById('heroParticleCanvas');
  if (heroCanvas) {
    const ctx = heroCanvas.getContext('2d');
    let width = heroCanvas.width = heroCanvas.offsetWidth;
    let height = heroCanvas.height = heroCanvas.offsetHeight;

    window.addEventListener('resize', () => {
      width = heroCanvas.width = heroCanvas.offsetWidth;
      height = heroCanvas.height = heroCanvas.offsetHeight;
    });

    let mouse = { x: width / 2, y: height / 2, radius: 140 };
    const heroBanner = heroCanvas.closest('.hero-banner');
    if (heroBanner) {
      heroBanner.addEventListener('mousemove', (e) => {
        const rect = heroCanvas.getBoundingClientRect();
        mouse.x = e.clientX - rect.left;
        mouse.y = e.clientY - rect.top;
      });
    }

    const particles = Array.from({ length: 45 }, () => ({
      x: Math.random() * width,
      y: Math.random() * height,
      vx: (Math.random() - 0.5) * 0.8,
      vy: (Math.random() - 0.5) * 0.8,
      radius: Math.random() * 2.5 + 1,
      baseAlpha: Math.random() * 0.5 + 0.2
    }));

    function renderParticles() {
      ctx.clearRect(0, 0, width, height);

      particles.forEach(p => {
        p.x += p.vx;
        p.y += p.vy;

        if (p.x < 0 || p.x > width) p.vx *= -1;
        if (p.y < 0 || p.y > height) p.vy *= -1;

        const dx = mouse.x - p.x;
        const dy = mouse.y - p.y;
        const dist = Math.sqrt(dx * dx + dy * dy);

        let alpha = p.baseAlpha;
        if (dist < mouse.radius) {
          alpha = Math.min(1, p.baseAlpha + (1 - dist / mouse.radius) * 0.6);
        }

        ctx.beginPath();
        ctx.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(255, 255, 255, ${alpha})`;
        ctx.fill();
      });

      // Connect close particles with delicate light lines
      for (let i = 0; i < particles.length; i++) {
        for (let j = i + 1; j < particles.length; j++) {
          const dx = particles[i].x - particles[j].x;
          const dy = particles[i].y - particles[j].y;
          const dist = Math.sqrt(dx * dx + dy * dy);

          if (dist < 110) {
            ctx.beginPath();
            ctx.moveTo(particles[i].x, particles[i].y);
            ctx.lineTo(particles[j].x, particles[j].y);
            ctx.strokeStyle = `rgba(255, 255, 255, ${0.18 * (1 - dist / 110)})`;
            ctx.lineWidth = 0.8;
            ctx.stroke();
          }
        }
      }

      requestAnimationFrame(renderParticles);
    }

    renderParticles();
  }

  // =========================================================================
  // 12. OVERDRIVE: SCROLL PROGRESS INDICATOR
  // =========================================================================
  const progressBar = document.getElementById('scrollProgressBar');
  if (progressBar) {
    window.addEventListener('scroll', () => {
      const winScroll = document.documentElement.scrollTop || document.body.scrollTop;
      const height = document.documentElement.scrollHeight - document.documentElement.clientHeight;
      const scrolled = (winScroll / height) * 100;
      progressBar.style.width = scrolled + '%';
    }, { passive: true });
  }

  // =========================================================================
  // 13. OVERDRIVE: WEB AUDIO API FEEDBACK SOUND GENERATOR
  // =========================================================================
  window.playCartAudioFeedback = function () {
    try {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (!AudioCtx) return;
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
      osc.frequency.exponentialRampToValueAtTime(880, ctx.currentTime + 0.12); // A5

      gain.gain.setValueAtTime(0.12, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.14);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start();
      osc.stop(ctx.currentTime + 0.15);
    } catch (e) {
      // Quiet fail if audio context is blocked
    }
  };

  // Trigger audio feedback inside addToCartAjax
  const origAddToCart = window.addToCartAjax;
  if (origAddToCart) {
    window.addToCartAjax = function (variantId, quantity = 1, triggerButton = null) {
      if (typeof window.playCartAudioFeedback === 'function') {
        window.playCartAudioFeedback();
      }
      return origAddToCart(variantId, quantity, triggerButton);
    };
  }

  // =========================================================================
  // 14. OVERDRIVE: ANIMATED COUNTER ROLLUP
  // =========================================================================
  const counterObserver = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting && !entry.target.dataset.animated) {
        entry.target.dataset.animated = 'true';
        const targetVal = parseInt(entry.target.getAttribute('data-counter')) || 0;
        if (targetVal <= 0) return;

        let startVal = 0;
        const duration = 1200;
        const stepTime = 20;
        const steps = duration / stepTime;
        const increment = targetVal / steps;

        const timer = setInterval(() => {
          startVal += increment;
          if (startVal >= targetVal) {
            entry.target.textContent = targetVal.toLocaleString('vi-VN') + '+';
            clearInterval(timer);
          } else {
            entry.target.textContent = Math.floor(startVal).toLocaleString('vi-VN') + '+';
          }
        }, stepTime);
      }
    });
  }, { threshold: 0.5 });

  document.querySelectorAll('[data-counter]').forEach(el => counterObserver.observe(el));

  // =========================================================================
  // 15. BOOTSTRAP TOOLTIPS AUTO-INITIALIZATION
  // =========================================================================
  if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"], [title]');
    tooltipTriggerList.forEach(tooltipTriggerEl => {
      if (!tooltipTriggerEl.getAttribute('data-bs-original-title')) {
        new bootstrap.Tooltip(tooltipTriggerEl, {
          boundary: document.body,
          fallbackPlacements: ['bottom', 'top']
        });
      }
    });
  }

  // =========================================================================
  // 16. PARALLAX SCROLLING EFFECT
  // =========================================================================
  const parallaxElements = document.querySelectorAll('.parallax-slow');
  if (parallaxElements.length > 0) {
    window.addEventListener('scroll', () => {
      const scrolled = window.pageYOffset;
      parallaxElements.forEach(el => {
        const speed = parseFloat(el.getAttribute('data-parallax-speed')) || 0.15;
        el.style.transform = `translateY(${scrolled * speed}px)`;
      });
    }, { passive: true });
  }

  // =========================================================================
  // 17. HEART BEAT MICROINTERACTION ON WISHLIST
  // =========================================================================
  const originalToggleFav = window.toggleFavoriteAjax;
  if (originalToggleFav) {
    window.toggleFavoriteAjax = function (productId, buttonElem) {
      if (buttonElem) {
        buttonElem.classList.add('heart-beat-active');
        setTimeout(() => buttonElem.classList.remove('heart-beat-active'), 500);
      }
      return originalToggleFav(productId, buttonElem);
    };
  }
});

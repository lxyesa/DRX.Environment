/* ═══════════════════════════════════════════════════════════════════════
   DRX Landing Page — Behavior Layer
   - Mobile nav toggle
   - FAQ accordion
   - Scroll fade-up (IntersectionObserver)
   - Navbar background on scroll
   ═══════════════════════════════════════════════════════════════════════ */

(function () {
  'use strict';

  /* ── Mobile Nav Toggle ──────────────────────────────────────────── */
  var navToggle = document.getElementById('nav-toggle');
  var nav = document.getElementById('nav');

  if (navToggle && nav) {
    navToggle.addEventListener('click', function () {
      nav.classList.toggle('is-open');
    });
  }

  /* ── FAQ Accordion ──────────────────────────────────────────────── */
  var faqItems = document.querySelectorAll('.faq-item');

  faqItems.forEach(function (item) {
    var btn = item.querySelector('.faq-item__question');
    var answer = item.querySelector('.faq-item__answer');
    if (!btn || !answer) return;

    btn.addEventListener('click', function () {
      var isOpen = item.classList.contains('is-open');

      // close all others
      faqItems.forEach(function (other) {
        other.classList.remove('is-open');
        var otherAnswer = other.querySelector('.faq-item__answer');
        if (otherAnswer) otherAnswer.style.maxHeight = null;
      });

      if (!isOpen) {
        item.classList.add('is-open');
        answer.style.maxHeight = answer.scrollHeight + 'px';
      }
    });
  });

  /* ── Scroll Fade-Up (IntersectionObserver) ──────────────────────── */
  var fadeEls = document.querySelectorAll('.fade-up');

  if ('IntersectionObserver' in window) {
    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.15 }
    );

    fadeEls.forEach(function (el) {
      observer.observe(el);
    });
  } else {
    // fallback: show all immediately
    fadeEls.forEach(function (el) {
      el.classList.add('is-visible');
    });
  }

  /* ── Navbar scroll effect ───────────────────────────────────────── */
  var scrolled = false;
  window.addEventListener('scroll', function () {
    var shouldAddClass = window.scrollY > 20;
    if (shouldAddClass !== scrolled) {
      scrolled = shouldAddClass;
      nav.style.background = scrolled
        ? 'rgba(10, 10, 15, 0.95)'
        : 'rgba(10, 10, 15, 0.8)';
    }
  });
})();

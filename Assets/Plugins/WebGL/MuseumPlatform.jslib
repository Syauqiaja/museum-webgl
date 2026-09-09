mergeInto(LibraryManager.library, {

  // 1 when the browser looks like a touch device. A coarse pointer AND a real touch point keeps
  // a touchscreen laptop out; the user-agent test catches phones whose pointer media query lies.
  MuseumIsTouchDevice: function () {
    try {
      var coarse = !!(window.matchMedia && window.matchMedia('(pointer: coarse)').matches);
      var points = (navigator.maxTouchPoints || 0) > 0;
      var mobileUA = /Android|iPhone|iPad|iPod|Mobile|Silk/i.test(navigator.userAgent || '');
      return ((coarse && points) || mobileUA) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // Must be called from inside a user gesture. iPhone Safari has no Fullscreen API at all, so a
  // silent no-op there is the correct outcome, not an error.
  //
  // Deliberately NOT unityInstance.SetFullscreen(1): that fullscreens the <canvas>, and a
  // fullscreen canvas is the only element the browser renders. Unity's own soft keyboard is a
  // <div> it appends to document.body (MobileKeyboard.js, JS_MobileKeyboard_Show), so it would
  // sit outside the fullscreen subtree, never paint, and never take focus — no keyboard on any
  // text field. A canvas cannot host DOM children, so the fix is to fullscreen the document
  // element instead: body, and everything Unity appends to it, stays inside.
  MuseumRequestFullscreen: function () {
    try {
      var el = document.documentElement;
      var request = el.requestFullscreen || el.webkitRequestFullscreen;
      if (request) request.call(el);
    } catch (e) {
      // Denied or unsupported. The rotate overlay and the viewport rules carry it alone.
    }
  },

});

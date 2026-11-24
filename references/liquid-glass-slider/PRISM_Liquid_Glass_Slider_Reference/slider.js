/* Liquid glass slider reference — adapted from the supplied JavaScript. */
(() => {
  const slider = document.getElementById("slider");
  const progress = document.getElementById("progress");
  const thumb = document.getElementById("thumb");
  const valueOutput = document.getElementById("value");

  if (!slider || !progress || !thumb) return;

  let dragging = false;
  let sliderRect = slider.getBoundingClientRect();
  let value = 40;

  const clamp = (n, min, max) => Math.max(min, Math.min(max, n));

  function refreshRect() {
    sliderRect = slider.getBoundingClientRect();
  }

  function render(nextValue) {
    value = clamp(Math.round(nextValue), 0, 100);
    progress.style.width = `${value}%`;
    thumb.style.left = `${value}%`;
    slider.setAttribute("aria-valuenow", String(value));
    if (valueOutput) valueOutput.textContent = `${value}%`;
    slider.dispatchEvent(new CustomEvent("sliderinput", {
      bubbles: true,
      detail: { value }
    }));
  }

  function setFromClientX(clientX) {
    refreshRect();
    if (!sliderRect.width) return;
    const percent = ((clientX - sliderRect.left) / sliderRect.width) * 100;
    render(percent);
  }

  function startDrag(event) {
    dragging = true;
    thumb.classList.add("active");
    slider.setPointerCapture?.(event.pointerId);
    setFromClientX(event.clientX);
    event.preventDefault();
  }

  function moveDrag(event) {
    if (!dragging) return;
    setFromClientX(event.clientX);
  }

  function stopDrag() {
    dragging = false;
    thumb.classList.remove("active");
  }

  slider.addEventListener("pointerdown", startDrag);
  slider.addEventListener("pointermove", moveDrag);
  slider.addEventListener("pointerup", stopDrag);
  slider.addEventListener("pointercancel", stopDrag);
  slider.addEventListener("lostpointercapture", stopDrag);

  slider.addEventListener("keydown", (event) => {
    let next = value;
    if (event.key === "ArrowRight" || event.key === "ArrowUp") next += 1;
    else if (event.key === "ArrowLeft" || event.key === "ArrowDown") next -= 1;
    else if (event.key === "PageUp") next += 10;
    else if (event.key === "PageDown") next -= 10;
    else if (event.key === "Home") next = 0;
    else if (event.key === "End") next = 100;
    else return;
    event.preventDefault();
    render(next);
  });

  window.addEventListener("resize", refreshRect);
  render(40);

  // Integration hook for a host preview:
  // slider.addEventListener("sliderinput", e => console.log(e.detail.value));
})();

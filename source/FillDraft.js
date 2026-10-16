(() => {
  // Only transfer after an explicit button press. Never submit or overwrite a draft.
  if (location.protocol !== 'https:' || location.hostname !== 'chat.deepseek.com') return 'wrong-origin';
  const text = __DRAFT_JSON__;
  const candidates = [...document.querySelectorAll('textarea')].filter(el =>
    /DeepSeek/i.test(el.getAttribute('placeholder') || '') &&
    !el.disabled && !el.readOnly && el.getClientRects().length > 0);
  if (candidates.length !== 1) return 'missing';
  const input = candidates[0];
  if (input.value.trim()) return 'occupied';
  const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set;
  setter.call(input, text);
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.focus();
  return input.value === text ? 'ok' : 'failed';
})()

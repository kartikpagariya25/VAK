window.islaModules.renderers['chat-claude'] = (el) => {
  el.innerHTML = '<webview src="https://claude.ai" partition="persist:isla-claude"></webview>';
};
window.islaModules.renderers['chat-gpt'] = (el) => {
  el.innerHTML = '<webview src="https://chatgpt.com" partition="persist:isla-gpt"></webview>';
};
// Each partition keeps its own cookie jar, so logging in once on either
// site stays logged in across restarts, same as a normal browser profile.

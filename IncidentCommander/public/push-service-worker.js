/* global self */

self.addEventListener('push', (event) => {
  if (!event.data) {
    return;
  }

  let payload;
  try {
    payload = event.data.json();
  } catch {
    payload = { title: event.data.text() };
  }

  const title = payload.title || 'Notification';
  const actions = Array.isArray(payload.actions) ? payload.actions : null;
  const options = {
    body: payload.body,
    icon: payload.iconUrl,
    tag: payload.tag,
    // Device-level urgency mirrors the foreground path: High stays up, Low is quiet.
    requireInteraction: payload.priority === 'High' || undefined,
    silent: payload.priority === 'Low' || undefined,
    // Inline action buttons (id + title); the launchUrl per action is kept in data for the click handler.
    actions: actions ? actions.map((a) => ({ action: a.id, title: a.title })) : undefined,
    data: { launchUrl: payload.launchUrl ?? null, data: payload.data ?? null, actions },
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const d = event.notification.data || {};
  const action = event.action || null;
  let launchUrl = d.launchUrl || null;
  if (action && Array.isArray(d.actions)) {
    const hit = d.actions.find((a) => a.id === action);
    if (hit && hit.launchUrl) {
      launchUrl = hit.launchUrl;
    }
  }

  // Prefer handing the click to an open app window so the SPA routes it client-side (like the
  // foreground tap hook); fall back to a full navigation / new window when nothing is open.
  const message = {
    type: 'ikon.notification-click',
    launchUrl,
    data: d.data ?? null,
    action,
    tag: event.notification.tag || null,
  };

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      for (const client of clientList) {
        if ('focus' in client) {
          client.focus();
          if ('postMessage' in client) {
            client.postMessage(message);
          } else if (launchUrl && 'navigate' in client) {
            client.navigate(launchUrl);
          }
          return undefined;
        }
      }
      if (launchUrl && self.clients.openWindow) {
        return self.clients.openWindow(launchUrl);
      }
      return undefined;
    }),
  );
});

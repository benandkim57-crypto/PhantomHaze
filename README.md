# PhantomHaze

PhantomHaze is a .NET 10 WinForms app for Windows that keeps its own window
hidden from many screen capture workflows by using
`SetWindowDisplayAffinity(..., WDA_EXCLUDEFROMCAPTURE)`.

Current version: v1.2.1 beta.

It includes:
- Always-on protection toggle with status feedback.
- Built-in logging for protection and browser events.
- Optional Web View mode with an address bar, refresh, history, and zoom.
- Session-focused privacy controls for Web View (cookies and site data
  are cleared/blocked for the app session).

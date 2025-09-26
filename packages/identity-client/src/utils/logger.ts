// Debug logging utility
let isDebugEnabled = true // Temporarily enabled for debugging

function syncWindowDebugState() {
  if (typeof window !== 'undefined') {
    ;(window as any).__identityDebugEnabled = isDebugEnabled
  }
}

export function enableDebugLogging(enabled: boolean = true) {
  isDebugEnabled = enabled
  syncWindowDebugState()
  return isDebugEnabled
}

export function debugLog(message: string, ...args: any[]) {
  if (isDebugEnabled) {
    console.log(message, ...args)
  }
}

// Global flag to enable debug logging from browser console
if (typeof window !== 'undefined') {
  ;(window as any).__enableIdentityDebug = enableDebugLogging
  syncWindowDebugState()
}

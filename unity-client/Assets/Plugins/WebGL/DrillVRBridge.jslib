mergeInto(LibraryManager.library, {
  DrillVR_DispatchTelemetry: function (eventJsonPtr) {
    var jsonStr = UTF8ToString(eventJsonPtr);
    try {
      var data = JSON.parse(jsonStr);
      if (window.DrillVRBridge && typeof window.DrillVRBridge.onTelemetryReceived === 'function') {
        window.DrillVRBridge.onTelemetryReceived(data);
      }
      window.dispatchEvent(new CustomEvent('drillvr:telemetry', { detail: data }));
    } catch (e) {
      console.error('[DrillVR Bridge] Failed to parse telemetry JSON:', e);
    }
  },

  DrillVR_NotifyStepCompleted: function (stepNumber, score, durationSeconds, mistakeCount) {
    var detail = {
      stepNumber: stepNumber,
      score: score,
      durationSeconds: durationSeconds,
      mistakeCount: mistakeCount,
      timestamp: Date.now()
    };
    if (window.DrillVRBridge && typeof window.DrillVRBridge.onStepCompleted === 'function') {
      window.DrillVRBridge.onStepCompleted(detail);
    }
    window.dispatchEvent(new CustomEvent('drillvr:step_completed', { detail: detail }));
  },

  DrillVR_NotifyDrillEnded: function (totalScore, passed, timeRemaining) {
    var detail = {
      totalScore: totalScore,
      passed: passed === 1,
      timeRemaining: timeRemaining,
      timestamp: Date.now()
    };
    if (window.DrillVRBridge && typeof window.DrillVRBridge.onDrillEnded === 'function') {
      window.DrillVRBridge.onDrillEnded(detail);
    }
    window.dispatchEvent(new CustomEvent('drillvr:drill_ended', { detail: detail }));
  }
});


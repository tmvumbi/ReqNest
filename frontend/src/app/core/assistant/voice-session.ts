export interface VoiceSessionCallbacks {
  onUserTranscript(text: string): void;
  onAssistantDelta(text: string): void;
  onAssistantTranscript(text: string): void;
  onStateChange(state: 'connecting' | 'live' | 'stopped' | 'error', detail?: string): void;
}

// WebRTC session against the OpenAI Realtime API. Audio flows peer-to-peer;
// transcripts arrive over the data channel and are surfaced via callbacks.
export class VoiceSession {
  private connection: RTCPeerConnection | null = null;
  private micStream: MediaStream | null = null;
  private audioElement: HTMLAudioElement | null = null;

  constructor(private readonly callbacks: VoiceSessionCallbacks) {}

  async start(clientSecret: string, model: string): Promise<void> {
    this.callbacks.onStateChange('connecting');
    try {
      this.micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      this.callbacks.onStateChange('error', 'Microphone access was denied.');
      return;
    }

    const connection = new RTCPeerConnection();
    this.connection = connection;
    this.audioElement = new Audio();
    this.audioElement.autoplay = true;
    connection.ontrack = (event) => {
      if (this.audioElement) this.audioElement.srcObject = event.streams[0];
    };
    for (const track of this.micStream.getTracks()) {
      connection.addTrack(track, this.micStream);
    }

    const channel = connection.createDataChannel('oai-events');
    channel.onmessage = (event) => this.handleEvent(event.data as string);
    channel.onopen = () => this.callbacks.onStateChange('live');

    const offer = await connection.createOffer();
    await connection.setLocalDescription(offer);
    let response: Response;
    try {
      response = await fetch(`https://api.openai.com/v1/realtime/calls?model=${encodeURIComponent(model)}`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${clientSecret}`, 'Content-Type': 'application/sdp' },
        body: offer.sdp,
      });
    } catch {
      this.callbacks.onStateChange('error', 'Could not reach the realtime service.');
      this.stop();
      return;
    }

    if (!response.ok) {
      this.callbacks.onStateChange('error', `Realtime handshake failed (${response.status}).`);
      this.stop();
      return;
    }

    await connection.setRemoteDescription({ type: 'answer', sdp: await response.text() });
  }

  stop(): void {
    this.micStream?.getTracks().forEach((track) => track.stop());
    this.micStream = null;
    this.connection?.close();
    this.connection = null;
    if (this.audioElement) {
      this.audioElement.srcObject = null;
      this.audioElement = null;
    }
    this.callbacks.onStateChange('stopped');
  }

  private handleEvent(raw: string): void {
    let event: { type?: string; transcript?: string; delta?: string };
    try {
      event = JSON.parse(raw);
    } catch {
      return;
    }
    switch (event.type) {
      // User speech transcription (GA + beta names).
      case 'conversation.item.input_audio_transcription.completed':
        if (event.transcript?.trim()) this.callbacks.onUserTranscript(event.transcript.trim());
        break;
      // Assistant speech transcript deltas / completions (GA + beta names).
      case 'response.output_audio_transcript.delta':
      case 'response.audio_transcript.delta':
        if (event.delta) this.callbacks.onAssistantDelta(event.delta);
        break;
      case 'response.output_audio_transcript.done':
      case 'response.audio_transcript.done':
        if (event.transcript?.trim()) this.callbacks.onAssistantTranscript(event.transcript.trim());
        break;
    }
  }
}

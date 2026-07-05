import { StudioSignalRClient, ConnectionState, HubConnectionFactory, StateChangedMessage } from '../StudioSignalRClient';
import { HubConnectionState } from '@microsoft/signalr';

function makeConnection() {
  const handlers: Record<string, ((...args: unknown[]) => void)> = {};
  const messageHandlers: Record<string, ((...args: unknown[]) => void)> = {};

  return {
    state: HubConnectionState.Disconnected as HubConnectionState,
    start: jest.fn(async () => { (conn as { state: HubConnectionState }).state = HubConnectionState.Connected; }),
    stop: jest.fn(async () => { (conn as { state: HubConnectionState }).state = HubConnectionState.Disconnected; }),
    invoke: jest.fn().mockResolvedValue(undefined),
    on: jest.fn((event: string, handler: (...args: unknown[]) => void) => {
      messageHandlers[event] = handler;
    }),
    onreconnecting: jest.fn((h: () => void) => { handlers['reconnecting'] = h; }),
    onreconnected:  jest.fn((h: () => void) => { handlers['reconnected'] = h; }),
    onclose:        jest.fn((h: () => void) => { handlers['close'] = h; }),
    // test helpers
    simulateReconnecting: () => handlers['reconnecting']?.(),
    simulateReconnected:  () => handlers['reconnected']?.(),
    simulateClose:        () => handlers['close']?.(),
    simulateMessage: (msg: StateChangedMessage) => messageHandlers['StateChanged']?.(msg),
  };
}

const conn = makeConnection();

const factory: HubConnectionFactory = {
  build: jest.fn(() => conn as never),
};

const PROJECT = 'my-project';

describe('StudioSignalRClient', () => {
  let client: StudioSignalRClient;

  beforeEach(() => {
    jest.clearAllMocks();
    Object.assign(conn, makeConnection());
    (factory.build as jest.Mock).mockReturnValue(conn);
    client = new StudioSignalRClient('http://localhost:5100/hubs/project', factory);
  });

  it('starts in disconnected state', () => {
    expect(client.connectionState).toBe<ConnectionState>('disconnected');
  });

  it('transitions disconnected → connecting → connected on start()', async () => {
    const states: ConnectionState[] = [];
    client.onConnectionStateChanged(s => states.push(s));

    await client.start(PROJECT);

    expect(states).toEqual<ConnectionState[]>(['connecting', 'connected']);
    expect(client.connectionState).toBe<ConnectionState>('connected');
  });

  it('joins the project group after connecting', async () => {
    await client.start(PROJECT);
    expect(conn.invoke).toHaveBeenCalledWith('JoinProject', PROJECT);
  });

  it('transitions to connecting on reconnecting event', async () => {
    await client.start(PROJECT);
    const states: ConnectionState[] = [];
    client.onConnectionStateChanged(s => states.push(s));

    conn.simulateReconnecting();
    expect(states).toContain<ConnectionState>('connecting');
  });

  it('transitions to connected on reconnected event', async () => {
    await client.start(PROJECT);
    conn.simulateReconnecting();
    const states: ConnectionState[] = [];
    client.onConnectionStateChanged(s => states.push(s));

    conn.simulateReconnected();
    expect(states).toContain<ConnectionState>('connected');
  });

  it('transitions to disconnected on close event', async () => {
    await client.start(PROJECT);
    const states: ConnectionState[] = [];
    client.onConnectionStateChanged(s => states.push(s));

    conn.simulateClose();
    expect(states).toContain<ConnectionState>('disconnected');
  });

  it('fires onAgentsChanged when Agents kind received', async () => {
    await client.start(PROJECT);
    let fired = false;
    client.onAgentsChanged(() => { fired = true; });

    conn.simulateMessage({ projectSlug: PROJECT, kinds: ['Agents'] });
    expect(fired).toBe(true);
  });

  it('fires onMessagesChanged when Messages kind received', async () => {
    await client.start(PROJECT);
    let fired = false;
    client.onMessagesChanged(() => { fired = true; });

    conn.simulateMessage({ projectSlug: PROJECT, kinds: ['Messages'] });
    expect(fired).toBe(true);
  });

  it('fires onDecisionsChanged when Decisions kind received', async () => {
    await client.start(PROJECT);
    let fired = false;
    client.onDecisionsChanged(() => { fired = true; });

    conn.simulateMessage({ projectSlug: PROJECT, kinds: ['Decisions'] });
    expect(fired).toBe(true);
  });

  it('ignores StateChanged messages for a different project', async () => {
    await client.start(PROJECT);
    let fired = false;
    client.onAgentsChanged(() => { fired = true; });

    conn.simulateMessage({ projectSlug: 'other-project', kinds: ['Agents'] });
    expect(fired).toBe(false);
  });

  it('can fire multiple kinds in one message', async () => {
    await client.start(PROJECT);
    let agentsFired = false, messagesFired = false;
    client.onAgentsChanged(() => { agentsFired = true; });
    client.onMessagesChanged(() => { messagesFired = true; });

    conn.simulateMessage({ projectSlug: PROJECT, kinds: ['Agents', 'Messages'] });
    expect(agentsFired).toBe(true);
    expect(messagesFired).toBe(true);
  });

  it('stop() leaves the project group and stops the connection', async () => {
    await client.start(PROJECT);
    await client.stop();

    expect(conn.invoke).toHaveBeenCalledWith('LeaveProject', PROJECT);
    expect(conn.stop).toHaveBeenCalled();
    expect(client.connectionState).toBe<ConnectionState>('disconnected');
  });
});

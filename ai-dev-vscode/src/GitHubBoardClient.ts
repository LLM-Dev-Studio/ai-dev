import type { BoardColumnItem, BoardData, BoardTaskItem } from './types';

const GITHUB_API = 'https://api.github.com';
const GITHUB_GRAPHQL = 'https://api.github.com/graphql';

const COL_BACKLOG = 'backlog';
const COL_IN_PROGRESS = 'in-progress';
const COL_REVIEW = 'review';
const COL_DONE = 'done';

const COLUMN_LABEL: Partial<Record<string, string>> = {
  [COL_IN_PROGRESS]: 'in-progress',
  [COL_REVIEW]: 'review',
};

// ── REST types ────────────────────────────────────────────────────────────────

interface GitHubIssue {
  number: number;
  node_id: string;
  title: string;
  body: string | null;
  state: string;
  labels: Array<{ name: string }>;
  assignees: Array<{ login: string }>;
  created_at: string;
  closed_at: string | null;
  pull_request?: unknown;
}

// ── GraphQL types ─────────────────────────────────────────────────────────────

interface GqlFieldValue {
  name?: string;
  field?: { name?: string };
}

interface GqlProjectItem {
  id: string;
  fieldValues: { nodes: GqlFieldValue[] };
  content: {
    id?: string;
    number?: number;
    title?: string;
    body?: string | null;
    state?: string;
    createdAt?: string;
    closedAt?: string | null;
    assignees?: { nodes: Array<{ login: string }> };
    labels?: { nodes: Array<{ name: string }> };
  } | null;
}

interface GqlStatusField {
  id: string;
  name: string;
  options: Array<{ id: string; name: string }>;
}

interface GqlProjectV2 {
  id: string;
  fields: { nodes: Array<Partial<GqlStatusField>> };
  items: { nodes: GqlProjectItem[] };
}

// ── Project cache ─────────────────────────────────────────────────────────────

interface ProjectCache {
  projectId: string;
  statusFieldId: string;
  /** column id → status option id */
  optionByColumn: Map<string, string>;
  /** issue number → project item id */
  itemIdByIssue: Map<number, string>;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function mapStatusToColumn(statusName: string): string {
  const s = statusName.toLowerCase().trim();
  if (s.includes('progress') || s === 'doing' || s === 'wip' || s === 'started') return COL_IN_PROGRESS;
  if (s.includes('review') || s === 'pr open' || s.includes('pr')) return COL_REVIEW;
  if (s === 'done' || s.includes('done') || s.includes('complet') || s.includes('shipped') || s.includes('closed') || s.includes('finish')) return COL_DONE;
  return COL_BACKLOG;
}

function buildProjectCache(project: GqlProjectV2): ProjectCache {
  const statusField = project.fields.nodes.find(
    f => f.id && f.name?.toLowerCase() === 'status' && f.options,
  ) as GqlStatusField | undefined;

  if (!statusField) {
    throw new Error('Project has no "Status" single-select field.');
  }

  const optionByColumn = new Map<string, string>();
  for (const opt of statusField.options) {
    const col = mapStatusToColumn(opt.name);
    if (!optionByColumn.has(col)) {
      optionByColumn.set(col, opt.id);
    }
  }

  const itemIdByIssue = new Map<number, string>();
  for (const item of project.items.nodes) {
    if (item.content?.number) {
      itemIdByIssue.set(item.content.number, item.id);
    }
  }

  return { projectId: project.id, statusFieldId: statusField.id, optionByColumn, itemIdByIssue };
}

function resolveColumnFromLabels(issue: GitHubIssue): string {
  if (issue.state === 'closed') return COL_DONE;
  const names = new Set(issue.labels.map(l => l.name.toLowerCase()));
  if (names.has('in-progress')) return COL_IN_PROGRESS;
  if (names.has('review')) return COL_REVIEW;
  return COL_BACKLOG;
}

function toTask(issue: Partial<GitHubIssue> & { number: number; title: string }): BoardTaskItem {
  return {
    id: String(issue.number),
    title: issue.title,
    priority: 'normal',
    description: issue.body ?? undefined,
    assignee: issue.assignees?.[0]?.login,
    tags: (issue.labels ?? [])
      .map(l => l.name)
      .filter(n => n !== 'in-progress' && n !== 'review'),
    createdAt: issue.created_at,
    completedAt: issue.closed_at ?? undefined,
  };
}

// ── GraphQL query/mutation strings ────────────────────────────────────────────

// Fetches the first Projects v2 linked to this repository (requires 'project' OAuth scope)
const AUTO_DISCOVER_PROJECT_QUERY = `
query AutoDiscoverProject($owner: String!, $repo: String!) {
  repository(owner: $owner, name: $repo) {
    projectsV2(first: 1) {
      nodes {
        id
        fields(first: 20) {
          nodes {
            ... on ProjectV2SingleSelectField { id name options { id name } }
          }
        }
        items(first: 100) {
          nodes {
            id
            fieldValues(first: 8) {
              nodes {
                ... on ProjectV2ItemFieldSingleSelectValue {
                  name
                  field { ... on ProjectV2SingleSelectField { name } }
                }
              }
            }
            content {
              ... on Issue {
                id number title body state createdAt closedAt
                assignees(first: 5) { nodes { login } }
                labels(first: 10) { nodes { name } }
              }
            }
          }
        }
      }
    }
  }
}`;

const ADD_TO_PROJECT_MUTATION = `
mutation AddToProject($projectId: ID!, $contentId: ID!) {
  addProjectV2ItemById(input: { projectId: $projectId contentId: $contentId }) {
    item { id }
  }
}`;

const SET_STATUS_MUTATION = `
mutation SetStatus($projectId: ID!, $itemId: ID!, $fieldId: ID!, $optionId: String!) {
  updateProjectV2ItemFieldValue(input: {
    projectId: $projectId
    itemId: $itemId
    fieldId: $fieldId
    value: { singleSelectOptionId: $optionId }
  }) {
    projectV2Item { id }
  }
}`;

// ── GitHubBoardClient ─────────────────────────────────────────────────────────

export class GitHubBoardClient {
  private projectCache?: ProjectCache;

  constructor(
    private readonly owner: string,
    private readonly repo: string,
    private readonly token: string,
  ) {}

  // ── Public board API ────────────────────────────────────────────────────────

  async getBoard(): Promise<BoardData> {
    const [open, closed] = await Promise.all([
      this.listIssues('open'),
      this.listIssues('closed'),
    ]);
    const issues = [...open, ...closed].filter(i => !i.pull_request);

    const columns: BoardColumnItem[] = [
      { id: COL_BACKLOG, title: 'Backlog', taskIds: [] },
      { id: COL_IN_PROGRESS, title: 'In Progress', taskIds: [] },
      { id: COL_REVIEW, title: 'Review', taskIds: [] },
      { id: COL_DONE, title: 'Done', taskIds: [] },
    ];
    const colMap = new Map(columns.map(c => [c.id, c]));
    const tasks: Record<string, BoardTaskItem> = {};

    const project = await this.tryAutoDiscoverProject();
    if (project) {
      this.projectCache = buildProjectCache(project);

      const columnByIssue = new Map<number, string>();
      for (const item of project.items.nodes) {
        if (!item.content?.number) continue;
        const statusValue = item.fieldValues.nodes.find(
          fv => fv.field?.name?.toLowerCase() === 'status',
        );
        if (statusValue?.name) {
          columnByIssue.set(item.content.number, mapStatusToColumn(statusValue.name));
        }
      }

      for (const issue of issues) {
        const task = toTask(issue);
        const columnId = columnByIssue.get(issue.number) ?? resolveColumnFromLabels(issue);
        colMap.get(columnId)?.taskIds.push(task.id);
        tasks[task.id] = task;
      }
    } else {
      for (const issue of issues) {
        const task = toTask(issue);
        colMap.get(resolveColumnFromLabels(issue))?.taskIds.push(task.id);
        tasks[task.id] = task;
      }
    }

    return { columns, tasks };
  }

  async createIssue(columnId: string, title: string, description?: string): Promise<BoardTaskItem> {
    const issue = await this.callRest<GitHubIssue>(
      'POST',
      `/repos/${this.owner}/${this.repo}/issues`,
      { title, body: description ?? '' },
    );

    if (this.projectCache) {
      await this.addIssueToProjectAndSetStatus(issue.node_id, issue.number, columnId);
    } else {
      const colLabel = COLUMN_LABEL[columnId];
      if (colLabel) {
        await this.callRest('PUT', `/repos/${this.owner}/${this.repo}/issues/${issue.number}/labels`, {
          labels: [colLabel],
        });
      }
    }

    return toTask(issue);
  }

  async updateIssue(
    issueNumber: string,
    columnId: string,
    title: string,
    description?: string,
  ): Promise<BoardTaskItem> {
    const num = parseInt(issueNumber, 10);
    const state = columnId === COL_DONE ? 'closed' : 'open';

    const issue = await this.callRest<GitHubIssue>(
      'PATCH',
      `/repos/${this.owner}/${this.repo}/issues/${num}`,
      { title, body: description ?? '', state },
    );

    if (this.projectCache) {
      await this.setProjectItemStatus(num, columnId);
    } else {
      const baseLabels = issue.labels.map(l => l.name).filter(n => n !== 'in-progress' && n !== 'review');
      const colLabel = state !== 'closed' ? COLUMN_LABEL[columnId] : undefined;
      const newLabels = colLabel ? [...baseLabels, colLabel] : baseLabels;
      await this.callRest('PUT', `/repos/${this.owner}/${this.repo}/issues/${num}/labels`, {
        labels: newLabels,
      });
      return toTask({ ...issue, state, labels: newLabels.map(n => ({ name: n })) });
    }

    return toTask({ ...issue, state });
  }

  async closeIssue(issueNumber: string): Promise<void> {
    const num = parseInt(issueNumber, 10);
    await this.callRest('PATCH', `/repos/${this.owner}/${this.repo}/issues/${num}`, {
      state: 'closed',
    });
    if (this.projectCache) {
      await this.setProjectItemStatus(num, COL_DONE);
    }
  }

  // ── Project auto-discovery ──────────────────────────────────────────────────

  private async tryAutoDiscoverProject(): Promise<GqlProjectV2 | null> {
    try {
      const result = await this.callGraphQL<{
        repository?: { projectsV2?: { nodes: GqlProjectV2[] } };
      }>(AUTO_DISCOVER_PROJECT_QUERY, { owner: this.owner, repo: this.repo });
      return result.repository?.projectsV2?.nodes?.[0] ?? null;
    } catch {
      // No 'project' OAuth scope, no linked projects, or network error — fall back to labels
      return null;
    }
  }

  // ── Project operations ──────────────────────────────────────────────────────

  private async addIssueToProjectAndSetStatus(
    nodeId: string,
    issueNumber: number,
    columnId: string,
  ): Promise<void> {
    if (!this.projectCache) return;

    const addResult = await this.callGraphQL<{
      addProjectV2ItemById: { item: { id: string } };
    }>(ADD_TO_PROJECT_MUTATION, {
      projectId: this.projectCache.projectId,
      contentId: nodeId,
    });

    const itemId = addResult.addProjectV2ItemById.item.id;
    this.projectCache.itemIdByIssue.set(issueNumber, itemId);
    await this.setProjectItemStatusById(itemId, columnId);
  }

  private async setProjectItemStatus(issueNumber: number, columnId: string): Promise<void> {
    if (!this.projectCache) return;
    const itemId = this.projectCache.itemIdByIssue.get(issueNumber);
    if (!itemId) return;
    await this.setProjectItemStatusById(itemId, columnId);
  }

  private async setProjectItemStatusById(itemId: string, columnId: string): Promise<void> {
    if (!this.projectCache) return;
    const optionId = this.projectCache.optionByColumn.get(columnId);
    if (!optionId) return;

    await this.callGraphQL(SET_STATUS_MUTATION, {
      projectId: this.projectCache.projectId,
      itemId,
      fieldId: this.projectCache.statusFieldId,
      optionId,
    });
  }

  // ── REST helpers ────────────────────────────────────────────────────────────

  private async listIssues(state: 'open' | 'closed'): Promise<GitHubIssue[]> {
    return this.callRest<GitHubIssue[]>(
      'GET',
      `/repos/${this.owner}/${this.repo}/issues?state=${state}&per_page=100&sort=created&direction=desc`,
    );
  }

  private async callRest<T>(method: string, path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${GITHUB_API}${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${this.token}`,
        Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });

    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`GitHub REST ${method} ${path} → ${res.status}: ${text}`);
    }
    if (res.status === 204) return undefined as T;
    return res.json() as Promise<T>;
  }

  private async callGraphQL<T>(query: string, variables: Record<string, unknown>): Promise<T> {
    const res = await fetch(GITHUB_GRAPHQL, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ query, variables }),
    });

    if (!res.ok) {
      const text = await res.text().catch(() => '');
      throw new Error(`GitHub GraphQL error ${res.status}: ${text}`);
    }

    const data = await res.json() as { data?: T; errors?: Array<{ message: string }> };
    if (data.errors?.length) {
      throw new Error(`GitHub GraphQL: ${data.errors.map(e => e.message).join('; ')}`);
    }
    return data.data as T;
  }
}

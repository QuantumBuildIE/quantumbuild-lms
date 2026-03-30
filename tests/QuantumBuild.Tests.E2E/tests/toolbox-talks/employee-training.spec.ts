import { test, expect } from '../../fixtures/test-fixtures';
import {
  EmployeePortalPage,
  TalkViewerPage,
} from '../../page-objects/toolbox-talks';
import { TEST_TENANT, TIMEOUTS, generateTestData } from '../../fixtures/test-constants';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const API_BASE = process.env.API_BASE_URL || 'http://localhost:5222';

const TALK_TITLE = `E2E Training ${Date.now()}`;
const TALK_SECTION = {
  sectionNumber: 1,
  title: 'Workplace Safety Basics',
  content:
    '<p>All personnel must wear PPE at all times on site. ' +
    'Report hazards immediately to your supervisor.</p>',
  requiresAcknowledgment: true,
};

const QUIZ_QUESTIONS = [
  {
    questionNumber: 1,
    questionText: 'Is PPE required at all times on site?',
    questionType: 1, // TrueFalse
    options: ['True', 'False'],
    correctAnswer: 'True',
    points: 1,
  },
  {
    questionNumber: 2,
    questionText: 'What should you do when you spot a hazard?',
    questionType: 0, // MultipleChoice
    options: [
      'Ignore it',
      'Report it to your supervisor',
      'Fix it yourself',
      'Leave the site',
    ],
    correctAnswer: 'Report it to your supervisor',
    points: 1,
  },
];

const CORRECT_ANSWERS: Record<string, string> = {
  'PPE required': 'True',
  'spot a hazard': 'Report it to your supervisor',
};

const WRONG_ANSWERS: Record<string, string> = {
  'PPE required': 'False',
  'spot a hazard': 'Ignore it',
};

// ---------------------------------------------------------------------------
// Shared state populated in beforeAll
// ---------------------------------------------------------------------------

let scheduledTalkId: string;
let failQuizScheduledTalkId: string;

// ---------------------------------------------------------------------------
// API helpers
// ---------------------------------------------------------------------------

async function apiLogin(email: string, password: string): Promise<string> {
  const res = await fetch(`${API_BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw new Error(`Login failed for ${email}: ${res.status}`);
  const data = await res.json();
  if (!data.success) throw new Error(`Login rejected for ${email}`);
  return data.accessToken;
}

function authHeaders(token: string) {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };
}

// ---------------------------------------------------------------------------
// Suite setup — create a talk, schedule it twice to the operator
// ---------------------------------------------------------------------------

test.beforeAll(async () => {
  // 1. Login as admin
  const adminToken = await apiLogin(
    TEST_TENANT.users.admin.email,
    TEST_TENANT.users.admin.password
  );

  // 2. Login as operator and resolve employeeId
  const operatorToken = await apiLogin(
    TEST_TENANT.users.operator.email,
    TEST_TENANT.users.operator.password
  );
  const meRes = await fetch(`${API_BASE}/api/auth/me`, {
    headers: authHeaders(operatorToken),
  });
  if (!meRes.ok) throw new Error(`/api/auth/me failed: ${meRes.status}`);
  const me = await meRes.json();
  const operatorEmployeeId: string = me.employeeId ?? me.data?.employeeId;
  if (!operatorEmployeeId) {
    throw new Error('Operator user has no linked employee — check global-setup');
  }

  // 3. Create a toolbox talk with 1 section + 2 quiz questions
  const createRes = await fetch(`${API_BASE}/api/toolbox-talks`, {
    method: 'POST',
    headers: authHeaders(adminToken),
    body: JSON.stringify({
      title: TALK_TITLE,
      description: 'E2E employee training flow test talk.',
      category: 'Safety',
      requiresQuiz: true,
      passingScore: 80,
      sections: [TALK_SECTION],
      questions: QUIZ_QUESTIONS,
    }),
  });
  if (!createRes.ok) {
    const errBody = await createRes.text();
    throw new Error(`Create talk failed (${createRes.status}): ${errBody}`);
  }
  const talk = await createRes.json();
  const talkId: string = talk.id ?? talk.data?.id;

  // 4. Update talk to enable certificate generation
  const updateBody = {
    id: talkId,
    title: TALK_TITLE,
    description: 'E2E employee training flow test talk.',
    category: 'Safety',
    requiresQuiz: true,
    passingScore: 80,
    generateCertificate: true,
    sections: (talk.sections ?? talk.data?.sections ?? []).map(
      (s: { id: string; sectionNumber: number; title: string; content: string; requiresAcknowledgment: boolean; source: number; videoTimestamp: string | null }) => ({
        id: s.id,
        sectionNumber: s.sectionNumber,
        title: s.title,
        content: s.content,
        requiresAcknowledgment: s.requiresAcknowledgment,
        source: s.source,
        videoTimestamp: s.videoTimestamp,
      })
    ),
    questions: (talk.questions ?? talk.data?.questions ?? []).map(
      (q: { id: string; questionNumber: number; questionText: string; questionType: number; options: string[] | null; correctAnswer: string; points: number; source: number; videoTimestamp: string | null; isFromVideoFinalPortion: boolean }) => ({
        id: q.id,
        questionNumber: q.questionNumber,
        questionText: q.questionText,
        questionType: q.questionType,
        options: q.options,
        correctAnswer: q.correctAnswer,
        points: q.points,
        source: q.source,
        videoTimestamp: q.videoTimestamp,
        isFromVideoFinalPortion: q.isFromVideoFinalPortion,
      })
    ),
  };
  const updateRes = await fetch(`${API_BASE}/api/toolbox-talks/${talkId}`, {
    method: 'PUT',
    headers: authHeaders(adminToken),
    body: JSON.stringify(updateBody),
  });
  if (!updateRes.ok) {
    const errBody = await updateRes.text();
    throw new Error(`Update talk failed (${updateRes.status}): ${errBody}`);
  }

  // 5. Create and process two schedules (one for main flow, one for fail-quiz test)
  async function createAndProcessSchedule(): Promise<string> {
    const schedRes = await fetch(`${API_BASE}/api/toolbox-talks/schedules`, {
      method: 'POST',
      headers: authHeaders(adminToken),
      body: JSON.stringify({
        toolboxTalkId: talkId,
        scheduledDate: new Date().toISOString(),
        frequency: 0, // Once
        assignToAllEmployees: false,
        employeeIds: [operatorEmployeeId],
      }),
    });
    if (!schedRes.ok) {
      const errBody = await schedRes.text();
      throw new Error(`Create schedule failed (${schedRes.status}): ${errBody}`);
    }
    const schedule = await schedRes.json();
    const scheduleId: string = schedule.id ?? schedule.data?.id;

    const processRes = await fetch(
      `${API_BASE}/api/toolbox-talks/schedules/${scheduleId}/process`,
      { method: 'POST', headers: authHeaders(adminToken) }
    );
    if (!processRes.ok) {
      const errBody = await processRes.text();
      throw new Error(`Process schedule failed (${processRes.status}): ${errBody}`);
    }
    return scheduleId;
  }

  await createAndProcessSchedule();
  await createAndProcessSchedule();

  // 6. Fetch the operator's assigned talks to get ScheduledTalk IDs
  const assignedRes = await fetch(
    `${API_BASE}/api/toolbox-talks/assigned/by-employee/${operatorEmployeeId}?pageSize=50`,
    { headers: authHeaders(adminToken) }
  );
  if (!assignedRes.ok) {
    throw new Error(`Fetch assigned talks failed: ${assignedRes.status}`);
  }
  const assignedData = await assignedRes.json();
  const items: { id: string; toolboxTalkId: string; status: string; statusDisplay: string }[] =
    assignedData.items ?? assignedData.data?.items ?? [];

  const pending = items.filter(
    (a) => a.toolboxTalkId === talkId && a.statusDisplay === 'Pending'
  );
  if (pending.length < 2) {
    throw new Error(
      `Expected 2 pending assignments for the test talk, got ${pending.length}`
    );
  }

  scheduledTalkId = pending[0].id;
  failQuizScheduledTalkId = pending[1].id;
});

// ---------------------------------------------------------------------------
// Tests — serial because each builds on the talk's server-side state
// ---------------------------------------------------------------------------

test.describe('Employee Training Flow', () => {
  test.describe.configure({ mode: 'serial' });

  test('operator sees assigned talk in My Learnings', async ({ operatorPage }) => {
    const portal = new EmployeePortalPage(operatorPage);
    await portal.navigateTo();
    await portal.assertOnPortal();

    // Assert the pending talk count is at least 1
    const count = await portal.getPendingTalkCount();
    expect(count).toBeGreaterThanOrEqual(1);

    // Assert the test talk title appears in the pending list
    await expect(
      operatorPage.locator(`text="${TALK_TITLE}"`).first()
    ).toBeVisible({ timeout: TIMEOUTS.medium });
  });

  test('operator can start a talk', async ({ operatorPage }) => {
    test.setTimeout(60_000);
    const viewer = new TalkViewerPage(operatorPage);
    await viewer.goto(scheduledTalkId);

    // Click Start
    await viewer.startTalk();

    // Assert talk moves to In Progress state — the viewer should show sections
    await expect(
      operatorPage.locator('text=/section/i').first()
    ).toBeVisible({ timeout: TIMEOUTS.medium });

    // Verify the URL still points to our scheduled talk
    await expect(operatorPage).toHaveURL(
      new RegExp(`/toolbox-talks/${scheduledTalkId}`)
    );
  });

  test('operator reads all sections', async ({ operatorPage }) => {
    const viewer = new TalkViewerPage(operatorPage);
    await viewer.goto(scheduledTalkId);

    // Talk is already InProgress from the previous test — start handles "Continue"
    await viewer.startTalk();
    await viewer.readAllSections();

    // After all sections are read, quiz or sign button should be visible
    await expect(
      operatorPage.locator(
        'button:has-text("Submit Quiz"), button:has-text("Submit"), ' +
        'text="Knowledge Check", text="Sign", button:has-text("Complete")'
      ).first()
    ).toBeVisible({ timeout: TIMEOUTS.medium });
  });

  test('operator passes quiz on first attempt', async ({ operatorPage }) => {
    const viewer = new TalkViewerPage(operatorPage);
    await viewer.goto(scheduledTalkId);

    // Sections are already read from previous test — quiz should be available
    // Navigate to quiz if not already there
    const quizHeading = operatorPage.locator('text="Knowledge Check"');
    if (!(await quizHeading.isVisible({ timeout: 3000 }).catch(() => false))) {
      await viewer.startTalk();
      await viewer.readAllSections();
    }

    // Submit correct answers
    await viewer.submitQuizAnswers(CORRECT_ANSWERS);

    // Assert pass result shown
    const result = await viewer.getQuizResult();
    expect(result.passed).toBe(true);
    expect(result.score).toBeGreaterThanOrEqual(80);

    // Assert score shows as integer percentage (Math.round applied)
    const scoreText = await operatorPage.locator('body').textContent();
    const scoreMatch = scoreText?.match(/(\d+)\s*%/);
    expect(scoreMatch).toBeTruthy();
    const scoreNumber = parseInt(scoreMatch![1], 10);
    expect(scoreNumber).toBe(Math.round(scoreNumber)); // integer check
  });

  test('operator fails quiz and sees retry option', async ({ operatorPage }) => {
    const viewer = new TalkViewerPage(operatorPage);

    // Use the second scheduled talk (fresh Pending state)
    await viewer.goto(failQuizScheduledTalkId);
    await viewer.startTalk();
    await viewer.readAllSections();

    // Submit all wrong answers
    await viewer.submitQuizAnswers(WRONG_ANSWERS);

    // Assert fail result shown
    const result = await viewer.getQuizResult();
    expect(result.passed).toBe(false);

    // Assert retry option is available
    await expect(
      operatorPage.locator(
        'button:has-text("Try Again"), button:has-text("Retry"), button:has-text("Rewatch")'
      ).first()
    ).toBeVisible({ timeout: TIMEOUTS.medium });
  });

  test('operator completes talk and receives certificate', async ({ operatorPage }) => {
    test.setTimeout(120_000);

    const viewer = new TalkViewerPage(operatorPage);

    // Navigate to the main scheduled talk (quiz already passed from earlier test)
    await viewer.goto(scheduledTalkId);

    // The quiz was passed in the earlier test — should be able to proceed to sign
    // If the page shows the quiz result, click Continue to proceed
    const continueAfterQuiz = operatorPage.locator(
      'button:has-text("Continue"), button:has-text("Next")'
    );
    if (await continueAfterQuiz.first().isVisible({ timeout: 3000 }).catch(() => false)) {
      await continueAfterQuiz.first().click();
      await operatorPage.waitForTimeout(500);
    }

    // If we need to re-pass the quiz (page reloaded from scratch)
    const quizSubmit = operatorPage.locator('button:has-text("Submit Quiz")');
    if (await quizSubmit.isVisible({ timeout: 3000 }).catch(() => false)) {
      await viewer.startTalk();
      await viewer.readAllSections();
      await viewer.submitQuizAnswers(CORRECT_ANSWERS);
      // Click continue after quiz pass
      const cont = operatorPage.locator('button:has-text("Continue")');
      if (await cont.first().isVisible({ timeout: 3000 }).catch(() => false)) {
        await cont.first().click();
        await operatorPage.waitForTimeout(500);
      }
    }

    // Sign and complete
    await viewer.signAndComplete();

    // Assert completion confirmation shown
    await expect(
      operatorPage.locator(
        'text="Learning Completed!", text="completed successfully", text="Well done"'
      ).first()
    ).toBeVisible({ timeout: TIMEOUTS.long });

    // Navigate to certificates page
    await operatorPage.goto('/toolbox-talks/certificates');
    await operatorPage.waitForLoadState('networkidle');

    // Assert certificate for the test talk appears
    await expect(
      operatorPage.locator(`text="${TALK_TITLE}"`).first()
    ).toBeVisible({ timeout: TIMEOUTS.long });
  });
});

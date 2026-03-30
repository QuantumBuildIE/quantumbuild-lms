/**
 * Test constants and data identifiers for E2E tests
 * These should match the seeded test data in the database
 */
export const TEST_TENANT = {
  id: '11111111-1111-1111-1111-111111111111',

  users: {
    admin: {
      email: 'admin@quantumbuild.ie',
      password: 'Admin123!',
      homePage: '/dashboard',
    },
    warehouse: {
      email: 'warehouse@quantumbuild.ie',
      password: 'Warehouse123!',
      homePage: '/dashboard',
    },
    siteManager: {
      email: 'sitemanager@quantumbuild.ie',
      password: 'SiteManager123!',
      homePage: '/dashboard',
    },
    officeStaff: {
      email: 'office@quantumbuild.ie',
      password: 'Office123!',
      homePage: '/dashboard',
    },
    finance: {
      email: 'finance@quantumbuild.ie',
      password: 'Finance123!',
      homePage: '/dashboard',
    },
    supervisor: {
      email: 'supervisor@test.quantumbuild.ie',
      password: 'TestSupervisor123!',
      homePage: '/toolbox-talks',
    },
    operator: {
      email: 'operator@test.quantumbuild.ie',
      password: 'TestOperator123!',
      homePage: '/toolbox-talks',
    },
  },

  sites: {
    quantumBuild: { name: 'Quantum Build', location: 'Dublin' },
    southWestGate: { name: 'South West Gate', location: 'Cork' },
    marmaladeLane: { name: 'Marmalade Lane', location: 'Galway' },
  },

};

/**
 * Test data generation helpers
 */
export const generateTestData = {
  uniqueString: (prefix: string = 'test') =>
    `${prefix}_${Date.now()}_${Math.random().toString(36).substring(7)}`,

  uniqueEmail: () =>
    `test_${Date.now()}@test.quantumbuild.ie`,

  uniqueReference: (prefix: string) =>
    `${prefix}-${Date.now().toString().slice(-6)}`,
};

/**
 * API endpoints for test verification
 */
export const API_ENDPOINTS = {
  auth: {
    login: '/api/auth/login',
    me: '/api/auth/me',
    refresh: '/api/auth/refresh-token',
  },
  toolboxTalks: {
    talks: '/api/toolbox-talks',
    subtitleProcess: '/toolbox-talks/{id}/subtitles/process',
    subtitleStatus: '/toolbox-talks/{id}/subtitles/status',
    subtitleCancel: '/toolbox-talks/{id}/subtitles/cancel',
    availableLanguages: '/api/subtitles/available-languages',
  },
  admin: {
    users: '/api/users',
    roles: '/api/roles',
    sites: '/api/sites',
    employees: '/api/employees',
    companies: '/api/companies',
    contacts: '/api/contacts',
  },
};

/**
 * Frontend routes
 */
export const ROUTES = {
  dashboard: '/dashboard',
  toolboxTalks: {
    employeePortal: '/toolbox-talks',
    adminDashboard: '/admin/toolbox-talks',
    talksList: '/admin/toolbox-talks/talks',
    createTalk: '/admin/toolbox-talks/talks/new',
    courses: '/admin/toolbox-talks/courses',
    schedules: '/admin/toolbox-talks/schedules',
    assignments: '/admin/toolbox-talks/assignments',
    reports: '/admin/toolbox-talks/reports',
    certificates: '/admin/toolbox-talks/certificates',
    settings: '/admin/toolbox-talks/settings',
    compliance: '/admin/toolbox-talks/compliance',
    pendingMappings: '/admin/toolbox-talks/pending-mappings',
  },
  admin: {
    home: '/admin',
    sites: '/admin/sites',
    employees: '/admin/employees',
    companies: '/admin/companies',
    users: '/admin/users',
  },
};

/**
 * Wait times and timeouts
 */
export const TIMEOUTS = {
  short: 5000,
  medium: 10000,
  long: 30000,
  navigation: 15000,
  api: 10000,
};

/**
 * Test tags for filtering
 */
export const TAGS = {
  smoke: '@smoke',
  regression: '@regression',
  critical: '@critical',
  slow: '@slow',
  flaky: '@flaky',
};

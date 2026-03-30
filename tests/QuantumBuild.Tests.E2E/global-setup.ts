import { FullConfig } from '@playwright/test';

const API_BASE = process.env.API_BASE_URL || 'http://localhost:5222';

const SEED_ADMIN = {
  email: 'admin@quantumbuild.ai',
  password: 'Admin123!',
};

const TEST_USERS = [
  { email: 'supervisor@test.quantumbuild.ie', password: 'TestSupervisor123!', firstName: 'Test', lastName: 'Supervisor', role: 'Supervisor', empCode: 'E2E-SUP' },
  { email: 'operator@test.quantumbuild.ie', password: 'TestOperator123!', firstName: 'Test', lastName: 'Operator', role: 'Operator', empCode: 'E2E-OPR' },
];

async function globalSetup(_config: FullConfig) {
  console.log('[global-setup] Ensuring E2E test users exist...');

  // 1. Login as existing admin
  const loginRes = await fetch(`${API_BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: SEED_ADMIN.email, password: SEED_ADMIN.password }),
  });

  if (!loginRes.ok) {
    throw new Error(`[global-setup] Admin login failed (${loginRes.status}). Is the API running at ${API_BASE}?`);
  }

  const loginData = await loginRes.json();
  if (!loginData.success) {
    throw new Error(`[global-setup] Admin login failed: ${loginData.errors?.join(', ')}`);
  }

  const token = loginData.accessToken;
  const authHeaders = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };

  // 2. Get roles to map role names to IDs
  const rolesRes = await fetch(`${API_BASE}/api/roles`, { headers: authHeaders });
  if (!rolesRes.ok) {
    throw new Error(`[global-setup] Failed to fetch roles (${rolesRes.status})`);
  }
  const rolesData = await rolesRes.json();
  const roles: { id: string; name: string }[] = rolesData.data || rolesData;
  const roleMap = new Map(roles.map((r) => [r.name, r.id]));

  // 3. Check existing users
  const usersRes = await fetch(`${API_BASE}/api/users?pageSize=100`, { headers: authHeaders });
  if (!usersRes.ok) {
    throw new Error(`[global-setup] Failed to fetch users (${usersRes.status})`);
  }
  const usersData = await usersRes.json();
  const existingEmails = new Set(
    (usersData.data?.items || usersData.items || []).map((u: { email: string }) => u.email.toLowerCase())
  );

  // 4. Create missing test users
  for (const user of TEST_USERS) {
    if (existingEmails.has(user.email.toLowerCase())) {
      console.log(`[global-setup] User ${user.email} already exists, skipping`);
      continue;
    }

    const roleId = roleMap.get(user.role);
    if (!roleId) {
      console.warn(`[global-setup] Role "${user.role}" not found, skipping user ${user.email}`);
      continue;
    }

    const createRes = await fetch(`${API_BASE}/api/users`, {
      method: 'POST',
      headers: authHeaders,
      body: JSON.stringify({
        email: user.email,
        firstName: user.firstName,
        lastName: user.lastName,
        password: user.password,
        confirmPassword: user.password,
        isActive: true,
        roleIds: [roleId],
        employeeLinkOption: 2,
        newEmployee: {
          employeeCode: user.empCode,
          phone: null,
          mobile: null,
          jobTitle: user.role,
          department: 'E2E Testing',
          primarySiteId: null,
        },
      }),
    });

    if (createRes.ok) {
      console.log(`[global-setup] Created user ${user.email} (${user.role})`);
    } else {
      const errBody = await createRes.text();
      console.warn(`[global-setup] Failed to create ${user.email} (${createRes.status}): ${errBody}`);
    }
  }

  // 5. Verify test users have linked employee records (fixes users created before employee linking was added)
  for (const user of TEST_USERS) {
    // Login as the test user to check their employeeId
    const checkLoginRes = await fetch(`${API_BASE}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: user.email, password: user.password }),
    });

    if (!checkLoginRes.ok) {
      console.warn(`[global-setup] Could not login as ${user.email} to verify employee link, skipping`);
      continue;
    }

    const checkLoginData = await checkLoginRes.json();
    if (!checkLoginData.success) {
      console.warn(`[global-setup] Login failed for ${user.email}: ${checkLoginData.errors?.join(', ')}`);
      continue;
    }

    const meRes = await fetch(`${API_BASE}/api/auth/me`, {
      headers: { Authorization: `Bearer ${checkLoginData.accessToken}` },
    });

    if (!meRes.ok) continue;

    const meData = await meRes.json();
    if (meData.employeeId) {
      console.log(`[global-setup] ${user.email} already has employeeId=${meData.employeeId}`);
      continue;
    }

    // User has no linked employee — create one and link it via admin
    console.log(`[global-setup] ${user.email} has no employeeId, creating and linking employee...`);

    // Create employee record
    const createEmpRes = await fetch(`${API_BASE}/api/employees`, {
      method: 'POST',
      headers: authHeaders,
      body: JSON.stringify({
        firstName: user.firstName,
        lastName: user.lastName,
        email: user.email,
        employeeCode: user.empCode,
        jobTitle: user.role,
        department: 'E2E Testing',
        isActive: true,
        createUserAccount: false,
        preferredLanguage: 'en',
      }),
    });

    if (!createEmpRes.ok) {
      const errBody = await createEmpRes.text();
      console.warn(`[global-setup] Failed to create employee for ${user.email} (${createEmpRes.status}): ${errBody}`);
      continue;
    }

    const empData = await createEmpRes.json();
    const employeeId = empData.data?.id || empData.id;

    if (!employeeId) {
      console.warn(`[global-setup] Employee created but no ID returned for ${user.email}`);
      continue;
    }

    // Link employee to user via POST /api/employees/{id}/link-user
    const linkRes = await fetch(`${API_BASE}/api/employees/${employeeId}/link-user`, {
      method: 'POST',
      headers: authHeaders,
      body: JSON.stringify({ userId: meData.id }),
    });

    if (linkRes.ok) {
      console.log(`[global-setup] Linked employee ${employeeId} to user ${user.email}`);
    } else {
      const errBody = await linkRes.text();
      console.warn(`[global-setup] Failed to link employee to ${user.email} (${linkRes.status}): ${errBody}`);
    }
  }

  // 6. Ensure DPA is accepted for the tenant
  const dpaStatusRes = await fetch(`${API_BASE}/api/dpa/status`, { headers: authHeaders });
  if (dpaStatusRes.ok) {
    const dpaStatus = await dpaStatusRes.json();
    if (!dpaStatus.accepted) {
      console.log('[global-setup] DPA not accepted, accepting now...');
      const dpaAcceptRes = await fetch(`${API_BASE}/api/dpa/accept`, {
        method: 'POST',
        headers: authHeaders,
        body: JSON.stringify({
          organisationLegalName: 'E2E Test Organisation',
          signatoryFullName: 'Test Admin',
          signatoryRole: 'Administrator',
          companyRegistrationNo: null,
          country: 'Ireland',
        }),
      });
      if (dpaAcceptRes.ok) {
        console.log('[global-setup] DPA accepted');
      } else {
        const errBody = await dpaAcceptRes.text();
        console.warn(`[global-setup] Failed to accept DPA (${dpaAcceptRes.status}): ${errBody}`);
      }
    } else {
      console.log('[global-setup] DPA already accepted');
    }
  }

  console.log('[global-setup] Test user seeding complete');
}

export default globalSetup;

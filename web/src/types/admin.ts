export interface Site {
  id: string;
  siteCode: string;
  siteName: string;
  address?: string;
  city?: string;
  postalCode?: string;
  siteManagerId?: string;
  siteManagerName?: string;
  companyId?: string;
  companyName?: string;
  phone?: string;
  email?: string;
  isActive: boolean;
  notes?: string;
}

export interface Employee {
  id: string;
  employeeCode: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email?: string;
  phone?: string;
  mobile?: string;
  jobTitle?: string;
  department?: string;
  primarySiteId?: string;
  primarySiteName?: string;
  startDate?: string;
  endDate?: string;
  isActive: boolean;
  notes?: string;
  /** Indicates whether this employee has a linked User account */
  hasUserAccount?: boolean;
  /** The linked User ID if a user account exists */
  linkedUserId?: string;
  /** Preferred language for Learning subtitles and notifications (ISO 639-1 code) */
  preferredLanguage: string;
}

export interface CreateEmployeeRequest {
  employeeCode: string;
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  mobile?: string;
  jobTitle?: string;
  department?: string;
  primarySiteId?: string;
  startDate?: string;
  endDate?: string;
  isActive: boolean;
  notes?: string;
  /** If true, creates a linked User account when Email is provided */
  createUserAccount?: boolean;
  /** Optional role name to assign to the created user */
  userRole?: string;
  /** Preferred language for Learning subtitles and notifications (ISO 639-1 code) */
  preferredLanguage?: string;
}

export interface Company {
  id: string;
  companyCode: string;
  companyName: string;
  tradingName?: string;
  phone?: string;
  email?: string;
  companyType?: string;
  isActive: boolean;
}

export interface Contact {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  companyId?: string;
  companyName?: string;
  siteId?: string;
  siteName?: string;
  isPrimaryContact: boolean;
  isActive: boolean;
  notes?: string;
}

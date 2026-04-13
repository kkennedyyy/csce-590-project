targetScope = 'resourceGroup'

@description('Azure Data Factory name for Sprint 2 external synchronization.')
param factoryName string

@description('Deployment location for Azure Data Factory resources.')
param location string = resourceGroup().location

@description('Base URL for the source scheduling API, for example https://func-class-scheduling-csce590.azurewebsites.net/api.')
param classApiBaseUrl string

@secure()
@description('Function key used for protected API routes.')
param classApiFunctionKey string

@secure()
@description('Container-level SAS URL for the students blob container.')
param studentsBlobSasUrl string

@secure()
@description('Container-level SAS URL for the professors blob container.')
param professorsBlobSasUrl string

@secure()
@description('Azure SQL connection string used by the ADF sink linked service.')
param sqlConnectionString string

@description('How often the external sync trigger runs.')
param scheduleIntervalMinutes int = 30

@description('UTC start time for the schedule trigger.')
param scheduleStartTime string = '2026-04-01T00:00:00Z'

var studentsBlobQueryIndex = indexOf(studentsBlobSasUrl, '?')
var studentsBlobFileUrl = studentsBlobQueryIndex >= 0
  ? '${substring(studentsBlobSasUrl, 0, studentsBlobQueryIndex)}/students.csv${substring(studentsBlobSasUrl, studentsBlobQueryIndex, length(studentsBlobSasUrl) - studentsBlobQueryIndex)}'
  : '${studentsBlobSasUrl}/students.csv'

var professorsBlobQueryIndex = indexOf(professorsBlobSasUrl, '?')
var professorsBlobFileUrl = professorsBlobQueryIndex >= 0
  ? '${substring(professorsBlobSasUrl, 0, professorsBlobQueryIndex)}/professors.json${substring(professorsBlobSasUrl, professorsBlobQueryIndex, length(professorsBlobSasUrl) - professorsBlobQueryIndex)}'
  : '${professorsBlobSasUrl}/professors.json'

resource factory 'Microsoft.DataFactory/factories@2018-06-01' = {
  name: factoryName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
}

resource sqlLinkedService 'Microsoft.DataFactory/factories/linkedservices@2018-06-01' = {
  name: 'ls_classfinder_azure_sql'
  parent: factory
  properties: {
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'AzureSqlDatabase'
    typeProperties: {
      connectionString: sqlConnectionString
    }
  }
}

resource apiLinkedService 'Microsoft.DataFactory/factories/linkedservices@2018-06-01' = {
  name: 'ls_classfinder_api_rest'
  parent: factory
  properties: {
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'RestService'
    typeProperties: {
      url: classApiBaseUrl
      enableServerCertificateValidation: true
      authenticationType: 'Anonymous'
    }
  }
}

resource studentsLinkedService 'Microsoft.DataFactory/factories/linkedservices@2018-06-01' = {
  name: 'ls_classfinder_students_http'
  parent: factory
  properties: {
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'HttpServer'
    typeProperties: {
      url: studentsBlobFileUrl
      enableServerCertificateValidation: true
      authenticationType: 'Anonymous'
    }
  }
}

resource professorsLinkedService 'Microsoft.DataFactory/factories/linkedservices@2018-06-01' = {
  name: 'ls_classfinder_professors_http'
  parent: factory
  properties: {
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'HttpServer'
    typeProperties: {
      url: professorsBlobFileUrl
      enableServerCertificateValidation: true
      authenticationType: 'Anonymous'
    }
  }
}

resource classesDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_classfinder_api_classes'
  parent: factory
  dependsOn: [
    apiLinkedService
  ]
  properties: {
    parameters: {
      offset: {
        type: 'String'
      }
    }
    linkedServiceName: {
      referenceName: 'ls_classfinder_api_rest'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'RestResource'
    typeProperties: {
      relativeUrl: {
        type: 'Expression'
        value: concat('''@concat('classes?limit=1000&offset=', dataset().offset, '&code=''', classApiFunctionKey, '''')''')
      }
    }
  }
}

resource enrollmentsDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_classfinder_api_enrollments'
  parent: factory
  dependsOn: [
    apiLinkedService
  ]
  properties: {
    parameters: {
      offset: {
        type: 'String'
      }
    }
    linkedServiceName: {
      referenceName: 'ls_classfinder_api_rest'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'RestResource'
    typeProperties: {
      relativeUrl: {
        type: 'Expression'
        value: concat('''@concat('enrollments?limit=1000&offset=', dataset().offset, '&code=''', classApiFunctionKey, '''')''')
      }
    }
  }
}

resource waitlistDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_classfinder_api_waitlist'
  parent: factory
  dependsOn: [
    apiLinkedService
  ]
  properties: {
    parameters: {
      offset: {
        type: 'String'
      }
    }
    linkedServiceName: {
      referenceName: 'ls_classfinder_api_rest'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'RestResource'
    typeProperties: {
      relativeUrl: {
        type: 'Expression'
        value: concat('''@concat('waitlist?limit=1000&offset=', dataset().offset, '&code=''', classApiFunctionKey, '''')''')
      }
    }
  }
}

resource studentsDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_classfinder_students_csv'
  parent: factory
  dependsOn: [
    studentsLinkedService
  ]
  properties: {
    linkedServiceName: {
      referenceName: 'ls_classfinder_students_http'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'DelimitedText'
    typeProperties: {
      location: {
        type: 'HttpServerLocation'
      }
      columnDelimiter: ','
      quoteChar: '"'
      escapeChar: '\\'
      firstRowAsHeader: true
    }
  }
}

resource professorsDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_classfinder_professors_json'
  parent: factory
  dependsOn: [
    professorsLinkedService
  ]
  properties: {
    linkedServiceName: {
      referenceName: 'ls_classfinder_professors_http'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    type: 'Json'
    typeProperties: {
      location: {
        type: 'HttpServerLocation'
      }
    }
  }
}

resource stageStudentsDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_stage_classfinder_students'
  parent: factory
  dependsOn: [
    sqlLinkedService
  ]
  properties: {
    linkedServiceName: {
      referenceName: 'ls_classfinder_azure_sql'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    schema: [
      {
        name: 'ExternalStudentId'
        type: 'String'
      }
      {
        name: 'FirstName'
        type: 'String'
      }
      {
        name: 'LastName'
        type: 'String'
      }
      {
        name: 'Email'
        type: 'String'
      }
      {
        name: 'Password'
        type: 'String'
      }
      {
        name: 'Major'
        type: 'String'
      }
      {
        name: 'Classification'
        type: 'String'
      }
    ]
    type: 'AzureSqlTable'
    typeProperties: {
      schema: 'dbo'
      table: 'StageClassFinderStudents'
    }
  }
}

resource stageProfessorsDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_stage_classfinder_professors'
  parent: factory
  dependsOn: [
    sqlLinkedService
  ]
  properties: {
    linkedServiceName: {
      referenceName: 'ls_classfinder_azure_sql'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    schema: [
      {
        name: 'ExternalProfessorId'
        type: 'String'
      }
      {
        name: 'FirstName'
        type: 'String'
      }
      {
        name: 'LastName'
        type: 'String'
      }
      {
        name: 'Email'
        type: 'String'
      }
      {
        name: 'Password'
        type: 'String'
      }
      {
        name: 'ClassesTaughtJson'
        type: 'String'
      }
    ]
    type: 'AzureSqlTable'
    typeProperties: {
      schema: 'dbo'
      table: 'StageClassFinderProfessors'
    }
  }
}

resource stageClassesDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_stage_classfinder_classes'
  parent: factory
  dependsOn: [
    sqlLinkedService
  ]
  properties: {
    linkedServiceName: {
      referenceName: 'ls_classfinder_azure_sql'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    schema: [
      {
        name: 'ExternalClassId'
        type: 'String'
      }
      {
        name: 'CourseCode'
        type: 'String'
      }
      {
        name: 'ClassName'
        type: 'String'
      }
      {
        name: 'Department'
        type: 'String'
      }
      {
        name: 'DepartmentCode'
        type: 'String'
      }
      {
        name: 'CourseNumber'
        type: 'Int32'
      }
      {
        name: 'SessionCode'
        type: 'String'
      }
      {
        name: 'Semester'
        type: 'String'
      }
      {
        name: 'ExternalProfessorId'
        type: 'String'
      }
      {
        name: 'ProfessorEmail'
        type: 'String'
      }
      {
        name: 'DaysOfWeekCompact'
        type: 'String'
      }
      {
        name: 'StartTime'
        type: 'String'
      }
      {
        name: 'EndTime'
        type: 'String'
      }
      {
        name: 'Location'
        type: 'String'
      }
      {
        name: 'MaxSeats'
        type: 'Int32'
      }
      {
        name: 'CurrentEnrolled'
        type: 'Int32'
      }
      {
        name: 'Credits'
        type: 'Int32'
      }
    ]
    type: 'AzureSqlTable'
    typeProperties: {
      schema: 'dbo'
      table: 'StageClassFinderClasses'
    }
  }
}

resource stageEnrollmentsDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_stage_classfinder_enrollments'
  parent: factory
  dependsOn: [
    sqlLinkedService
  ]
  properties: {
    linkedServiceName: {
      referenceName: 'ls_classfinder_azure_sql'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    schema: [
      {
        name: 'ExternalEnrollmentId'
        type: 'String'
      }
      {
        name: 'ExternalStudentId'
        type: 'String'
      }
      {
        name: 'ExternalClassId'
        type: 'String'
      }
      {
        name: 'EnrollmentDateUtc'
        type: 'DatetimeOffset'
      }
      {
        name: 'Status'
        type: 'String'
      }
    ]
    type: 'AzureSqlTable'
    typeProperties: {
      schema: 'dbo'
      table: 'StageClassFinderEnrollments'
    }
  }
}

resource stageWaitlistDataset 'Microsoft.DataFactory/factories/datasets@2018-06-01' = {
  name: 'ds_stage_classfinder_waitlist'
  parent: factory
  dependsOn: [
    sqlLinkedService
  ]
  properties: {
    linkedServiceName: {
      referenceName: 'ls_classfinder_azure_sql'
      type: 'LinkedServiceReference'
    }
    annotations: [
      'sprint2'
      'external-sync'
    ]
    schema: [
      {
        name: 'ExternalWaitlistId'
        type: 'String'
      }
      {
        name: 'ExternalStudentId'
        type: 'String'
      }
      {
        name: 'ExternalClassId'
        type: 'String'
      }
      {
        name: 'SignupDateUtc'
        type: 'DatetimeOffset'
      }
      {
        name: 'Position'
        type: 'Int32'
      }
    ]
    type: 'AzureSqlTable'
    typeProperties: {
      schema: 'dbo'
      table: 'StageClassFinderWaitlist'
    }
  }
}

resource externalSyncPipeline 'Microsoft.DataFactory/factories/pipelines@2018-06-01' = {
  name: 'pl_classfinder_external_sync'
  parent: factory
  dependsOn: [
    sqlLinkedService
    classesDataset
    enrollmentsDataset
    waitlistDataset
    studentsDataset
    professorsDataset
    stageStudentsDataset
    stageProfessorsDataset
    stageClassesDataset
    stageEnrollmentsDataset
    stageWaitlistDataset
  ]
  properties: {
    annotations: [
      'sprint2'
      'external-sync'
    ]
    folder: {
      name: 'Sprint2'
    }
    parameters: {
      pipelineRunId: {
        type: 'String'
        defaultValue: 'manual-run'
      }
    }
    activities: [
      {
        name: 'Begin Sync Run'
        type: 'SqlServerStoredProcedure'
        linkedServiceName: {
          referenceName: 'ls_classfinder_azure_sql'
          type: 'LinkedServiceReference'
        }
        typeProperties: {
          storedProcedureName: 'dbo.usp_ClassFinder_BeginExternalSync'
          storedProcedureParameters: {
            PipelineRunId: {
              value: {
                value: '@pipeline().parameters.pipelineRunId'
                type: 'Expression'
              }
              type: 'String'
            }
            Summary: {
              value: 'ADF staged copy is starting.'
              type: 'String'
            }
          }
        }
      }
      {
        name: 'Copy Students CSV To Stage'
        type: 'Copy'
        dependsOn: [
          {
            activity: 'Begin Sync Run'
            dependencyConditions: [
              'Succeeded'
            ]
          }
        ]
        inputs: [
          {
            referenceName: 'ds_classfinder_students_csv'
            type: 'DatasetReference'
          }
        ]
        outputs: [
          {
            referenceName: 'ds_stage_classfinder_students'
            type: 'DatasetReference'
          }
        ]
        typeProperties: {
          source: {
            type: 'HttpSource'
            httpRequestTimeout: '00:02:00'
          }
          sink: {
            type: 'AzureSqlSink'
            preCopyScript: 'TRUNCATE TABLE dbo.StageClassFinderStudents;'
            writeBehavior: 'insert'
            sqlWriterUseTableLock: true
          }
          translator: {
            type: 'TabularTranslator'
            mappings: [
              {
                source: {
                  name: 'student_id'
                }
                sink: {
                  name: 'ExternalStudentId'
                }
              }
              {
                source: {
                  name: 'first_name'
                }
                sink: {
                  name: 'FirstName'
                }
              }
              {
                source: {
                  name: 'last_name'
                }
                sink: {
                  name: 'LastName'
                }
              }
              {
                source: {
                  name: 'email'
                }
                sink: {
                  name: 'Email'
                }
              }
              {
                source: {
                  name: 'password'
                }
                sink: {
                  name: 'Password'
                }
              }
              {
                source: {
                  name: 'major'
                }
                sink: {
                  name: 'Major'
                }
              }
              {
                source: {
                  name: 'classification'
                }
                sink: {
                  name: 'Classification'
                }
              }
            ]
          }
        }
      }
      {
        name: 'Copy Professors JSON To Stage'
        type: 'Copy'
        dependsOn: [
          {
            activity: 'Begin Sync Run'
            dependencyConditions: [
              'Succeeded'
            ]
          }
        ]
        inputs: [
          {
            referenceName: 'ds_classfinder_professors_json'
            type: 'DatasetReference'
          }
        ]
        outputs: [
          {
            referenceName: 'ds_stage_classfinder_professors'
            type: 'DatasetReference'
          }
        ]
        typeProperties: {
          source: {
            type: 'HttpSource'
            httpRequestTimeout: '00:02:00'
          }
          sink: {
            type: 'AzureSqlSink'
            preCopyScript: 'TRUNCATE TABLE dbo.StageClassFinderProfessors;'
            writeBehavior: 'insert'
            sqlWriterUseTableLock: true
          }
          translator: {
            type: 'TabularTranslator'
            collectionReference: '$'
            mappings: [
              {
                source: {
                  path: '$.professor_id'
                }
                sink: {
                  name: 'ExternalProfessorId'
                }
              }
              {
                source: {
                  path: '$.first_name'
                }
                sink: {
                  name: 'FirstName'
                }
              }
              {
                source: {
                  path: '$.last_name'
                }
                sink: {
                  name: 'LastName'
                }
              }
              {
                source: {
                  path: '$.email'
                }
                sink: {
                  name: 'Email'
                }
              }
              {
                source: {
                  path: '$.password'
                }
                sink: {
                  name: 'Password'
                }
              }
            ]
          }
        }
      }
      {
        name: 'Copy Classes API To Stage'
        type: 'ForEach'
        dependsOn: [
          {
            activity: 'Begin Sync Run'
            dependencyConditions: [
              'Succeeded'
            ]
          }
        ]
        typeProperties: {
          isSequential: true
          items: {
            type: 'Expression'
            value: '@range(0, 51)'
          }
          activities: [
            {
              name: 'Copy Classes API Page To Stage'
              type: 'Copy'
              inputs: [
                {
                  referenceName: 'ds_classfinder_api_classes'
                  type: 'DatasetReference'
                  parameters: {
                    offset: {
                      type: 'Expression'
                      value: '@string(mul(item(), 1000))'
                    }
                  }
                }
              ]
              outputs: [
                {
                  referenceName: 'ds_stage_classfinder_classes'
                  type: 'DatasetReference'
                }
              ]
              typeProperties: {
                source: {
                  type: 'RestSource'
                  httpRequestTimeout: '00:02:00'
                  requestMethod: 'GET'
                }
                sink: {
                  type: 'AzureSqlSink'
                  preCopyScript: {
                    type: 'Expression'
                    value: '''@if(equals(item(), 0), 'TRUNCATE TABLE dbo.StageClassFinderClasses;', '')'''
                  }
                  writeBehavior: 'insert'
                  sqlWriterUseTableLock: true
                }
                translator: {
                  type: 'TabularTranslator'
                  collectionReference: '$.data'
                  mappings: [
                    {
                      source: {
                        path: 'id'
                      }
                      sink: {
                        name: 'ExternalClassId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'course_code'
                      }
                      sink: {
                        name: 'CourseCode'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'class_name'
                      }
                      sink: {
                        name: 'ClassName'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'department'
                      }
                      sink: {
                        name: 'Department'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'department_code'
                      }
                      sink: {
                        name: 'DepartmentCode'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'course_number'
                      }
                      sink: {
                        name: 'CourseNumber'
                        type: 'Int32'
                      }
                    }
                    {
                      source: {
                        path: 'session'
                      }
                      sink: {
                        name: 'SessionCode'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'semester'
                      }
                      sink: {
                        name: 'Semester'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'professor_id'
                      }
                      sink: {
                        name: 'ExternalProfessorId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'days_of_week'
                      }
                      sink: {
                        name: 'DaysOfWeekCompact'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'start_time'
                      }
                      sink: {
                        name: 'StartTime'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'end_time'
                      }
                      sink: {
                        name: 'EndTime'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'location'
                      }
                      sink: {
                        name: 'Location'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'max_seats'
                      }
                      sink: {
                        name: 'MaxSeats'
                        type: 'Int32'
                      }
                    }
                    {
                      source: {
                        path: 'current_enrolled'
                      }
                      sink: {
                        name: 'CurrentEnrolled'
                        type: 'Int32'
                      }
                    }
                    {
                      source: {
                        path: 'credits'
                      }
                      sink: {
                        name: 'Credits'
                        type: 'Int32'
                      }
                    }
                  ]
                }
              }
            }
          ]
        }
      }
      {
        name: 'Copy Enrollments API To Stage'
        type: 'ForEach'
        dependsOn: [
          {
            activity: 'Begin Sync Run'
            dependencyConditions: [
              'Succeeded'
            ]
          }
        ]
        typeProperties: {
          isSequential: true
          items: {
            type: 'Expression'
            value: '@range(0, 51)'
          }
          activities: [
            {
              name: 'Copy Enrollments API Page To Stage'
              type: 'Copy'
              inputs: [
                {
                  referenceName: 'ds_classfinder_api_enrollments'
                  type: 'DatasetReference'
                  parameters: {
                    offset: {
                      type: 'Expression'
                      value: '@string(mul(item(), 1000))'
                    }
                  }
                }
              ]
              outputs: [
                {
                  referenceName: 'ds_stage_classfinder_enrollments'
                  type: 'DatasetReference'
                }
              ]
              typeProperties: {
                source: {
                  type: 'RestSource'
                  httpRequestTimeout: '00:02:00'
                  requestMethod: 'GET'
                }
                sink: {
                  type: 'AzureSqlSink'
                  preCopyScript: {
                    type: 'Expression'
                    value: '''@if(equals(item(), 0), 'TRUNCATE TABLE dbo.StageClassFinderEnrollments;', '')'''
                  }
                  writeBehavior: 'insert'
                  sqlWriterUseTableLock: true
                }
                translator: {
                  type: 'TabularTranslator'
                  collectionReference: '$.data'
                  mappings: [
                    {
                      source: {
                        path: 'id'
                      }
                      sink: {
                        name: 'ExternalEnrollmentId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'student_id'
                      }
                      sink: {
                        name: 'ExternalStudentId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'class_id'
                      }
                      sink: {
                        name: 'ExternalClassId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'enrollment_date'
                      }
                      sink: {
                        name: 'EnrollmentDateUtc'
                        type: 'DatetimeOffset'
                      }
                    }
                    {
                      source: {
                        path: 'status'
                      }
                      sink: {
                        name: 'Status'
                        type: 'String'
                      }
                    }
                  ]
                }
              }
            }
          ]
        }
      }
      {
        name: 'Copy Waitlist API To Stage'
        type: 'ForEach'
        dependsOn: [
          {
            activity: 'Begin Sync Run'
            dependencyConditions: [
              'Succeeded'
            ]
          }
        ]
        typeProperties: {
          isSequential: true
          items: {
            type: 'Expression'
            value: '@range(0, 51)'
          }
          activities: [
            {
              name: 'Copy Waitlist API Page To Stage'
              type: 'Copy'
              inputs: [
                {
                  referenceName: 'ds_classfinder_api_waitlist'
                  type: 'DatasetReference'
                  parameters: {
                    offset: {
                      type: 'Expression'
                      value: '@string(mul(item(), 1000))'
                    }
                  }
                }
              ]
              outputs: [
                {
                  referenceName: 'ds_stage_classfinder_waitlist'
                  type: 'DatasetReference'
                }
              ]
              typeProperties: {
                source: {
                  type: 'RestSource'
                  httpRequestTimeout: '00:02:00'
                  requestMethod: 'GET'
                }
                sink: {
                  type: 'AzureSqlSink'
                  preCopyScript: {
                    type: 'Expression'
                    value: '''@if(equals(item(), 0), 'TRUNCATE TABLE dbo.StageClassFinderWaitlist;', '')'''
                  }
                  writeBehavior: 'insert'
                  sqlWriterUseTableLock: true
                }
                translator: {
                  type: 'TabularTranslator'
                  collectionReference: '$.data'
                  mappings: [
                    {
                      source: {
                        path: 'id'
                      }
                      sink: {
                        name: 'ExternalWaitlistId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'student_id'
                      }
                      sink: {
                        name: 'ExternalStudentId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'class_id'
                      }
                      sink: {
                        name: 'ExternalClassId'
                        type: 'String'
                      }
                    }
                    {
                      source: {
                        path: 'signup_date'
                      }
                      sink: {
                        name: 'SignupDateUtc'
                        type: 'DatetimeOffset'
                      }
                    }
                    {
                      source: {
                        path: 'position'
                      }
                      sink: {
                        name: 'Position'
                        type: 'Int32'
                      }
                    }
                  ]
                }
              }
            }
          ]
        }
      }
      {
        name: 'Apply External Snapshot To Curated Tables'
        type: 'SqlServerStoredProcedure'
        dependsOn: [
          {
            activity: 'Copy Students CSV To Stage'
            dependencyConditions: [
              'Succeeded'
            ]
          }
          {
            activity: 'Copy Professors JSON To Stage'
            dependencyConditions: [
              'Succeeded'
            ]
          }
          {
            activity: 'Copy Classes API To Stage'
            dependencyConditions: [
              'Succeeded'
            ]
          }
          {
            activity: 'Copy Enrollments API To Stage'
            dependencyConditions: [
              'Succeeded'
            ]
          }
          {
            activity: 'Copy Waitlist API To Stage'
            dependencyConditions: [
              'Succeeded'
            ]
          }
        ]
        linkedServiceName: {
          referenceName: 'ls_classfinder_azure_sql'
          type: 'LinkedServiceReference'
        }
        typeProperties: {
          storedProcedureName: 'dbo.usp_ClassFinder_ApplyExternalSync'
          storedProcedureParameters: {
            PipelineRunId: {
              value: {
                value: '@pipeline().parameters.pipelineRunId'
                type: 'Expression'
              }
              type: 'String'
            }
          }
        }
      }
    ]
  }
}

resource scheduleTrigger 'Microsoft.DataFactory/factories/triggers@2018-06-01' = {
  name: 'trg_classfinder_external_sync_schedule'
  parent: factory
  dependsOn: [
    externalSyncPipeline
  ]
  properties: {
    annotations: [
      'sprint2'
      'external-sync'
    ]
    pipelines: [
      {
        pipelineReference: {
          referenceName: 'pl_classfinder_external_sync'
          type: 'PipelineReference'
        }
        parameters: {
          pipelineRunId: {
            value: '@trigger().scheduledTime'
            type: 'Expression'
          }
        }
      }
    ]
    type: 'ScheduleTrigger'
    typeProperties: {
      recurrence: {
        frequency: 'Minute'
        interval: scheduleIntervalMinutes
        startTime: scheduleStartTime
        timeZone: 'UTC'
      }
    }
  }
}

output dataFactoryName string = factory.name
output triggerName string = scheduleTrigger.name

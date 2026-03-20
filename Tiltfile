docker_compose('docker-compose.yml')

dc_resource('api',
  trigger_mode=TRIGGER_MODE_AUTO,
  labels=['backend'])

dc_resource('web',
  trigger_mode=TRIGGER_MODE_AUTO,
  labels=['frontend'])

dc_resource('postgres', labels=['infra'])
dc_resource('redis', labels=['infra'])
dc_resource('pgadmin', labels=['infra'])
dc_resource('aspire-dashboard', labels=['infra'])

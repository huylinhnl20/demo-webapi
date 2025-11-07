# OpenShift Kustomize Deployment met Certificate File Watching

Deze configuratie bevat een complete Kustomize setup voor deployment van de MyApi applicatie in OpenShift, inclusief certificate management via secrets en symbolic links.

## Structuur

```
k8s/
├── base/                           # Base Kustomize configuratie
│   ├── deployment.yaml            # Base deployment configuratie
│   ├── service.yaml               # Service en Route configuratie
│   ├── configmap.yaml            # ConfigMap met appsettings
│   ├── secret.yaml               # TLS certificate secret
│   └── kustomization.yaml        # Base Kustomization
└── overlays/
    ├── development/               # Development environment
    │   ├── kustomization.yaml    
    │   ├── deployment-patch.yaml 
    │   └── configmap-patch.yaml  
    └── production/                # Production environment
        ├── kustomization.yaml    
        └── deployment-patch.yaml 
```

## OpenShift Certificate Management

### 1. TLS Secret maken

```bash
# Met bestaande certificaat bestanden
oc create secret tls myapi-tls-certs \
  --cert=server.crt \
  --key=server.key \
  --namespace=myapi-dev

# Of met oc apply
oc apply -f k8s/base/secret.yaml -n myapi-dev
```

### 2. Certificaat bestanden base64 encoderen

```bash
# Voor het updaten van secret.yaml
cat server.crt | base64 -w 0 > tls.crt.b64
cat server.key | base64 -w 0 > tls.key.b64
```

### 3. Secret mount gedrag in OpenShift

OpenShift mount secrets als symbolic links:

```
/app/certs/
├── ..data -> ..2023_11_06_12_30_45.123456789/
├── ..2023_11_06_12_30_45.123456789/
│   ├── tls.crt
│   └── tls.key
├── tls.crt -> ..data/tls.crt
└── tls.key -> ..data/tls.key
```

## Deployment Instructies

### Development Environment

```bash
# Namespace aanmaken
oc new-project myapi-dev

# Certificaten uploaden
oc create secret tls myapi-tls-certs-dev \
  --cert=certs/dev-server.crt \
  --key=certs/dev-server.key

# Deploy met Kustomize
oc apply -k k8s/overlays/development/

# Status controleren
oc get pods -n myapi-dev
oc logs -f deployment/myapi-myapi -n myapi-dev
```

### Production Environment

```bash
# Namespace aanmaken
oc new-project myapi-prod

# Certificaten uploaden (productie certificaten)
oc create secret tls myapi-tls-certs \
  --cert=certs/prod-server.crt \
  --key=certs/prod-server.key

# Deploy met Kustomize
oc apply -k k8s/overlays/production/

# Status controleren
oc get pods -n myapi-prod
oc get route -n myapi-prod
```

## Certificate File Watching Kenmerken

### Symbolic Link Monitoring

De applicatie is geconfigureerd om symbolic links te monitoren:

- **Directory watching**: Monitort de gehele `/app/certs` directory
- **..data detection**: Detecteert wanneer OpenShift de `..data` symbolic link bijwerkt
- **Change notifications**: Logt wanneer certificaten worden bijgewerkt
- **Restart indicator**: Geeft aan wanneer een herstart nodig is

### Configuratie in Program.cs

```csharp
// OpenShift/Kubernetes compatible file watching
fileSystemWatcher.Filter = "*"; // Watch all files
fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | 
                                NotifyFilters.CreationTime | 
                                NotifyFilters.Attributes;
```

## Certificate Renewal Process

### 1. Update Secret

```bash
# Nieuwe certificaten uploaden
oc create secret tls myapi-tls-certs \
  --cert=new-server.crt \
  --key=new-server.key \
  --dry-run=client -o yaml | oc apply -f -
```

### 2. Automatic Detection

De applicatie detecteert automatisch:
- Symbolic link wijzigingen
- Nieuwe certificate data
- File timestamp updates

### 3. Application Restart (indien nodig)

```bash
# Force restart van pods voor nieuwe certificaten
oc rollout restart deployment/myapi-myapi -n myapi-dev
```

## Environment Variables

### Development

```yaml
env:
- name: ASPNETCORE_ENVIRONMENT
  value: "Development"
- name: ASPNETCORE_URLS
  value: "https://+:8443;http://+:8080"
```

### Production

```yaml
env:
- name: ASPNETCORE_ENVIRONMENT
  value: "Production"
- name: ASPNETCORE_URLS
  value: "https://+:8443;http://+:8080"
```

## Security Instellingen

### Container Security Context

```yaml
securityContext:
  allowPrivilegeEscalation: false
  runAsNonRoot: true
  runAsUser: 1001
  readOnlyRootFilesystem: true
  capabilities:
    drop:
    - ALL
```

### Secret Mount Permissions

```yaml
volumes:
- name: tls-certs
  secret:
    secretName: myapi-tls-certs
    defaultMode: 0440  # Read-only voor owner en group
```

## Monitoring en Logging

### Health Checks

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
```

### Certificate Monitoring Logs

De applicatie logt:
- Certificate file wijzigingen
- Symbolic link updates
- File permission status
- Certificate validatie informatie

```
Certificate file or symbolic link ..data was updated. Application restart may be required for changes to take effect.
Certificate file exists: True, Last write: 06/11/2025 12:30:45
Certificate file is a symbolic link (OpenShift secret mount detected)
```

## Troubleshooting

### Common Issues

1. **Certificate mount fails**
   ```bash
   oc describe pod <pod-name>
   oc logs <pod-name>
   ```

2. **Symbolic link not detected**
   ```bash
   # Check mount point
   oc exec <pod-name> -- ls -la /app/certs/
   ```

3. **Permission errors**
   ```bash
   # Verify secret permissions
   oc get secret myapi-tls-certs -o yaml
   ```

### Debug Commands

```bash
# Check certificate expiration
oc exec <pod-name> -- openssl x509 -in /app/certs/tls.crt -text -noout

# Monitor file changes
oc exec <pod-name> -- watch ls -la /app/certs/

# Application logs
oc logs -f <pod-name> | grep -i cert
```

## Customization

### Andere Certificate Paths

Update in `configmap.yaml`:

```yaml
"Certificate": {
  "Path": "/app/certs/custom.crt",
  "KeyPath": "/app/certs/custom.key",
  "Port": 8443
}
```

### Extra Volumes

Add in `deployment.yaml`:

```yaml
volumeMounts:
- name: ca-certs
  mountPath: /app/ca-certs
  readOnly: true
volumes:
- name: ca-certs
  configMap:
    name: ca-certificates
```

## Best Practices

1. **Certificate Rotation**: Gebruik korte levensduur certificaten
2. **Secret Management**: Gebruik externe secret management tools
3. **Monitoring**: Implementeer certificate expiration monitoring
4. **Backup**: Bewaar backup van certificaten buiten cluster
5. **Testing**: Test certificate renewal proces regelmatig
# Ubuntu Package Downloader 설계서

## 1. 목적

본 프로그램은 Online PC에서 사용자가 선택한 Ubuntu OS Version, Architecture, Repository 조건에 맞는 Package 및 Package Dependency `.deb` 파일을 다운로드하여, Offline PC(Ubuntu)의 사전 구성된 Local-Repo에 투입 가능한 `.deb` 파일 세트를 생성한다.

기존 `NuGetDependencyDownloaderWPF`와 동일한 운영 환경을 전제로 한다.

- Online PC: Operation Program 실행 및 `.deb` 다운로드
- Offline PC(Ubuntu): 사전 구성된 Local-Repo에 `.deb` 파일 반영
- 전달 대상: `.deb` 파일
- 제외 대상: Offline PC Local-Repo 설정, Offline PC 설치 자동화, 원격 복사

## 2. 기본 운영 흐름

1. 사용자가 Ubuntu Version과 Architecture를 선택한다.
2. 사용자가 다운로드할 Package 목록과 Version을 입력하거나 import한다.
3. 프로그램은 Ubuntu 저장소의 `Packages.gz` metadata를 직접 조회한다.
4. 내장 resolver가 Debian package dependency 규칙을 사용하여 Package Dependency를 계산한다.
5. 미해결 dependency가 없으면 계산된 `.deb` 파일 목록을 즉시 다운로드한다.
6. 이미 존재하는 `.deb` 파일은 skip한다.
7. 다운로드 파일의 checksum을 검증한다.
8. 결과 파일과 report를 생성한다.
9. 사용자는 `.deb` 파일을 Offline PC의 Local-Repo 위치로 옮긴다.

## 3. 사용자 선택 항목

### 3.1 Ubuntu Version

- UI: `ComboBox`
- Autocomplete 지원
- 사용자가 선택 가능
- 예시:
  - `20.04 focal`
  - `22.04 jammy`
  - `24.04 noble`

### 3.2 Architecture

- UI: `ComboBox`
- Autocomplete 지원
- 사용자가 선택 가능
- 제공 목록:
  - `amd64`
  - `i386`
  - `arm64`
  - `armhf`
  - `ppc64el`
  - `s390x`
  - `riscv64`
- `all` 패키지는 선택 대상 Architecture가 아니며, 선택한 Architecture의 dependency를 해석할 때 `apt`가 자동으로 포함한다.
- `amd64`, `i386`은 Ubuntu Archive 계열 저장소를 사용한다.
- `arm64`, `armhf`, `ppc64el`, `s390x`, `riscv64`는 Ubuntu Ports 계열 저장소를 사용한다.
- Ubuntu Version에 따라 특정 Architecture 또는 Package가 제공되지 않을 수 있으며, 이 경우 dependency resolve 결과에 오류를 표시한다.

### 3.3 Repository Component

- UI: 편집 불가능한 `ComboBox`
- 제공 값은 권장 기본값으로 한정한다.

```text
main universe
```

### 3.4 Repository Pocket

- UI: 편집 불가능한 `ComboBox`
- 제공 값은 권장 기본값으로 한정한다.

```text
release updates security
```

### 3.5 Package 입력 방식

기존 `NuGetDependencyDownloaderWPF`와 동일한 방식을 따른다.

- 단건 Package 입력
- Package list import
- Package list export
- 목록 기반 다운로드
- 중복 제거
- 진행 상태 표시
- 실패 항목 retry
- 기존 파일 skip

## 4. Ubuntu Repository 정의

### 4.1 Server Repository 선택

사용자가 `Server Repository` ComboBox에서 metadata 및 `.deb` 다운로드의 시작 서버를 선택한다.

```text
Kakao Mirror (Ubuntu fallback):
https://mirror.kakao.com/ubuntu/

Ubuntu Official:
https://archive.ubuntu.com/ubuntu/
https://security.ubuntu.com/ubuntu/
```

- 기본 선택값: `Kakao Mirror (Ubuntu fallback)`
- `Ubuntu Official` 선택 시 Kakao mirror를 조회하지 않고 공식 서버부터 사용한다.

### 4.2 Fallback 정책

- `Kakao Mirror (Ubuntu fallback)` 선택 시 Metadata 조회는 Kakao mirror를 먼저 사용한다.
- Kakao mirror에서 선택한 Ubuntu Version, Architecture, Component, Pocket 조합의 metadata를 찾을 수 없으면 Ubuntu official server로 fallback한다.
- Ubuntu Ports Architecture는 Kakao의 일반 Ubuntu mirror를 먼저 시도한 뒤, `ports.ubuntu.com/ubuntu-ports`로 fallback한다.
- EOL Ubuntu의 Ports Architecture는 `old-releases.ports.ubuntu.com/ubuntu-ports`로 fallback한다.
- Kakao mirror에서 `.deb` 다운로드가 실패하면 Ubuntu official server에서 동일 파일 경로로 재시도한다.
- Fallback 발생 내역은 `manifest.json` 및 `download-report.csv`에 기록한다.

### 4.3 Security Pocket 처리

- Kakao mirror에서는 `noble-security`, `jammy-security` 같은 security pocket도 동일 base URL에서 먼저 시도한다.
- Kakao mirror 실패 시 official security server인 `https://security.ubuntu.com/ubuntu/`로 fallback한다.

## 5. Dependency Resolution

### 5.1 Resolver 방식

Online PC에 WSL 또는 Ubuntu의 `apt-get` 설치를 요구하지 않는다.

Windows 프로그램이 Ubuntu 저장소의 `Packages.gz` metadata를 HTTPS로 직접 내려받고, 내장 resolver로 dependency closure를 계산한다.

- Ubuntu Version
- Architecture
- Repository Component: `main universe`
- Repository Pocket: `release updates security`
- Repository Source
- Package Name
- Package Version

내장 resolver는 다음 Debian package metadata를 처리한다.

- `Package`
- `Version`
- `Architecture`
- `Filename`
- `Size`
- `Pre-Depends`
- `Depends`
- `Recommends`
- `Provides`

Dependency 대안, Architecture 조건, Package Version 조건 및 Debian Version 정렬 규칙을 적용하여 다운로드 대상 `.deb` 목록을 생성한다.

### 5.2 포함 Dependency 범위

기본 포함:

- `Depends`
- `Pre-Depends`

기본 제외:

- `Recommends`
- `Suggests`

`Recommends`는 사용자 옵션으로 포함할 수 있게 한다.

### 5.3 Package Version 선택

- 사용자는 available versions 목록에서 Package Version을 선택할 수 있다.
- 기본값은 `apt` candidate/latest version으로 한다.
- 사용자가 root package version을 명시하면 해당 version을 고정한다.
- Dependency package는 `apt` resolver 결과를 따른다.

### 5.4 OR Dependency 및 Virtual Package

- OR Dependency는 `apt` resolver 결과를 우선 사용한다.
- Virtual Package는 `apt` resolver가 선택한 provider를 우선 사용한다.
- 자동 해결이 불가능하거나 모호한 경우 unresolved 항목으로 기록한다.

### 5.5 Conflict 계열 정보

다운로드 프로그램은 실제 설치를 수행하지 않으므로 아래 항목은 기본적으로 차단 조건이 아니라 report 및 warning 대상으로 처리한다.

- `Conflicts`
- `Breaks`
- `Replaces`

## 6. Offline PC 전제

Offline PC에는 Local-Repo가 별도로 설정되어 있다고 가정한다.

프로그램 책임에서 제외되는 항목:

- Offline PC의 `/etc/apt/sources.list.d/...` 설정
- `dpkg-scanpackages` 또는 `apt-ftparchive` 실행
- `Packages.gz` 생성
- `apt update`
- 실제 설치 명령 실행
- Offline PC로 원격 복사

초기 버전에서는 Offline PC의 기존 설치 상태를 반영하지 않는다. 따라서 dependency 계산 결과에 포함된 `.deb` 파일을 충분히 다운로드하는 방식을 기본으로 한다.

향후 확장 옵션:

- Offline PC에서 export한 `dpkg-query` 결과 import
- 이미 설치된 package/version 제외

## 7. 다운로드 정책

### 7.1 이미 존재하는 파일 처리

이미 존재하는 `.deb` 파일은 기본적으로 skip한다.

- 동일 파일명이 존재하면 skip
- 가능하면 checksum을 확인하여 정상 파일인지 검증
- skip 결과도 `download-report.csv`에 기록

### 7.2 동시 다운로드

동시 다운로드 수는 최대값을 사용한다.

구현 시 권장 사항:

- 프로그램이 허용 가능한 최대 동시 다운로드 수 적용
- UI에서 제한값 조정 가능하도록 확장 가능
- 실패 시 개별 package 단위로 retry

### 7.3 Retry 및 실패 처리

- Kakao mirror 우선 시도
- 실패 시 Ubuntu official server fallback
- 다운로드 실패는 기록 후 가능한 항목은 계속 진행
- 최종 실패 항목은 UI와 report에 표시

### 7.4 Checksum 검증

- `SHA256` 검증을 필수로 한다.
- Checksum mismatch 발생 시 1회 재다운로드한다.
- 재다운로드 후에도 실패하면 실패 항목으로 기록한다.

## 8. 출력물 구조

출력 폴더는 사용자가 변경하지 않으며 실행 파일 위치를 기준으로 고정한다.

```text
<실행 파일 경로>/Download/
```

출력 구조:

```text
Download/
  *.deb
  manifest.json
  download-report.csv
  checksums.sha256
  unresolved-dependencies.csv
```

Offline PC로 전달하는 기본 대상은 `Download/` 폴더의 `.deb` 파일이다.

## 9. Report 파일

### 9.1 manifest.json

다운로드 조건과 resolution 결과를 기록한다.

주요 포함 항목:

- Ubuntu Version
- Architecture
- Components
- Pockets
- Repository source
- Fallback 발생 내역
- Requested packages
- Resolved packages
- File name
- Source URL
- SHA256
- CreatedAt

### 9.2 download-report.csv

권장 컬럼:

```text
Status,Package,Version,Architecture,Reason,FileName,SourceUrl,Size,SHA256,Message
```

Status 예시:

- `Downloaded`
- `Skipped`
- `Failed`
- `Verified`
- `FallbackUsed`

Reason 예시:

- `Requested`
- `Dependency`

### 9.3 checksums.sha256

파일 무결성 검증용으로 생성한다.

```text
<sha256>  <filename>.deb
```

### 9.4 unresolved-dependencies.csv

자동 해결 실패 항목을 기록한다.

권장 컬럼:

```text
Package,RequiredBy,Constraint,Reason,Candidates
```

## 10. GUI 설계 방향

기존 `NuGetDependencyDownloaderWPF`의 사용 흐름을 재사용한다.

주요 UI 요소:

- Ubuntu Version autocomplete `ComboBox`
- Architecture autocomplete `ComboBox`
- Components 고정 `ComboBox`: `main universe`
- Pockets 고정 `ComboBox`: `release updates security`
- Package 입력 영역
- Package list import/export
- Server Repository 선택
- 실행 파일 하위 `Download` 고정 경로 표시 및 Windows Explorer 열기
- Include Recommends checkbox
- Skip existing 표시
- 진행률 표시
- 다운로드 로그
상단에는 `Download` 버튼만 제공한다. `Download`는 dependency resolve와 `.deb` 다운로드를 연속 수행한다. 미해결 dependency가 있으면 report를 생성한 뒤 다운로드를 시작하지 않는다.

Report 행은 `Reason`에 따라 시각적으로 구분한다.

- 첫 번째 `No.` 열에 화면 표시 순서대로 `1, 2, 3...` 순번을 부여한다.
- 새 다운로드 작업을 시작해 Report를 초기화하면 순번도 1부터 다시 시작한다.
- 기본 창 폭은 Report의 `Message` 열까지 표시할 수 있도록 넓게 제공한다.
- `Message` 열은 최소 폭을 확보하여 내용이 완전히 가려지지 않게 한다.
- Package List 하단 입력 예시는 창 크기에 따라 자동 줄바꿈한다.
- `Requested`: 옅은 파란색 배경과 파란색 계열 글자
- `Dependency`: 옅은 초록색 배경과 초록색 계열 글자
- `Reason` 텍스트는 굵게 표시하여 색상 외에도 구분 가능하게 한다.

## 11. 사용자 설정 저장

최근 사용한 설정을 저장한다.

- Ubuntu Version
- Architecture
- Repository source
- Include Recommends
- 동시 다운로드 제한값이 제공되는 경우 해당 값

민감정보는 저장하지 않는다.

## 12. Proxy 및 보안

Proxy 설정은 지원 가능하도록 설계한다.

- HTTP/HTTPS proxy 입력 가능
- Proxy 인증 정보는 저장하지 않음
- Token, password, credential, `.env`, `profile.xml` 등 민감정보는 report, log, release asset에 포함하지 않음

## 13. 로그 정책

- UI 로그 제공
- 파일 로그 제공 가능
- Package name, version, source URL, 실패 사유 기록
- 인증 정보, token, password 기록 금지

## 14. 검증 계획

초기 smoke test는 대표 package 1개를 기준으로 수행한다.

검증 항목:

1. Ubuntu Version / Architecture 선택 및 고정 Component / Pocket 확인
2. Kakao mirror metadata 조회
3. Ubuntu official fallback 동작
4. Windows native metadata resolver 기반 dependency closure 계산
5. 미해결 dependency가 없을 때 `.deb` 즉시 다운로드
6. SHA256 검증
7. 이미 존재하는 파일 skip
8. `manifest.json` 생성
9. `download-report.csv` 생성
10. `checksums.sha256` 생성
11. unresolved dependency 기록
12. 실행 파일 하위 `Download` 고정 경로 사용

## 15. 범위 제외 항목

본 프로그램의 초기 범위에서 제외한다.

- Offline PC Local-Repo 생성 자동화
- Offline PC package 설치 자동화
- Offline PC 원격 접속
- Offline PC로 파일 자동 복사
- 실제 credential 저장
- build output, cache, runtime log를 배포 산출물에 포함

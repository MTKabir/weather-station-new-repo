# WeatherStation Image Processing – Azure Functions

## 📌 Overview

This project implements a serverless image processing pipeline using Azure Functions (.NET 8 Isolated).  

The system fetches real-time weather data for 40 weather stations from the Buienradar API, retrieves public images, overlays weather data onto each image, stores them in Azure Blob Storage, and exposes APIs to track job status and retrieve results.

---

## Architecture Overview

1. **HTTP Trigger** starts a new image generation job.
2. A **QueueTrigger** processes the job in the background.
3. Weather data is fetched from Buienradar API.
4. For each station, a fan-out job is added to a second queue.
5. Images are retrieved from a public API.
6. Weather data is written onto the image.
7. Processed images are stored in Azure Blob Storage.
8. A status API returns job progress and image URLs (SAS protected).

---

## MUST Requirements – Completed

###  Expose publicly accessible API for requesting fresh images
- `POST /api/jobs`
- Returns a unique `jobId`

###  Use QueueTrigger for background processing
- `job-start-queue`
- `image-process-queue`

###  Use Blob Storage
- Stores generated images
- SAS token used for secure access

###  Use Buienradar API
- https://data.buienradar.nl/2.0/feed/json
- Fetches 40 weather stations

###  Use public image API
- Picsum Photos (public placeholder image API)
- Weather data written on image using ImageSharp

###  Expose API for fetching results
- `GET /api/jobs/{jobId}`
- Returns:
  - Job status
  - Completed count
  - Total count
  - SAS image URLs

###  Provide HTTP file as documentation
- `weatherstation.http` included in repo

###  Provide Bicep template
- Creates:
  - Storage Account
  - Blob Container
  - Two Queues
  - Application Insights
  - Linux Consumption Plan
  - Function App (.NET Isolated 8)

###  Provide deploy.ps1 script
Script:
- Creates Resource Group
- Deploys Bicep template
- Publishes .NET project
- Zips correctly
- Deploys using Azure CLI

###  Use multiple queues
- Queue 1  start job
- Queue 2  process images

###  Deploy to Azure with working endpoint
- Functions deployed to Azure
- Public endpoints accessible

---

##  COULD Requirement – Implemented

### ✔ Use SAS Token for image access
Images are returned with time-limited read-only SAS tokens.



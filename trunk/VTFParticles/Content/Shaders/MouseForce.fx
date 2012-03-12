
texture temporaryMap;
sampler temporarySampler : register(s0) = sampler_state
{
	Texture = <temporaryMap>;
	MipFilter = None;
	MinFilter = Point;
	MagFilter = Point;
	AddressU = Clamp;
	AddressV = Clamp;
};

texture positionMap;
sampler positionSampler = sampler_state
{
	Texture = <positionMap>;
	MipFilter = None;
	MinFilter = Point;
	MagFilter = Point;
	AddressU = Clamp;
	AddressV = Clamp;
};

texture velocityMap;
sampler velocitySampler = sampler_state
{
	Texture = <velocityMap>;
	MipFilter = None;
	MinFilter = Point;
	MagFilter = Point;
	AddressU = Clamp;
	AddressV = Clamp;
};

texture randomMap;
sampler randomSampler : register(s0) = sampler_state
{
	Texture = <randomMap>;
	MipFilter = None;
	MinFilter = Point;
	MagFilter = Point;
	AddressU = wrap;
	AddressV = wrap;
};

float2 screenDimensions;// = float2(1280,720);
float3 halfDimensions;// = float2(screenDimensions.x/2, screenDimensions.y/2);

float maxLife = 5.0f;
//float3 generateNewPosition(float2)
//generate 3 dimensional coordinates based off whatever is in randomMap
float3 generateNewPosition(float2 uv)
{
	float4 rand = tex2D(randomSampler, uv);
	//return float3(rand.x*1024,rand.z*1024,rand.y*1024);
	//rand = normalize(rand);
	return float3(rand.x*screenDimensions.x,rand.y*screenDimensions.y,0);
}

//float4  ResetPositionPS(float2)
//generate new positions with the random data in randomMap. takes the uv coords of the vertex
//calculates a new lifetime based off the values in randomMap.
float4 ResetPositionPS(in float2 uv : TEXCOORD0) : COLOR
{
	return float4(generateNewPosition(uv), 
		maxLife*frac(tex2D(randomSampler, uv).w));
}

//float4 ResetVelocitiesPS(float2)
//generate velocities as float4. the generated velocities are all ZEROOOO!!!!!
float4 ResetVelocitiesPS(in float2 uv : TEXCOORD0) : COLOR
{
	//float4 rand = normalize(tex2D(randomSampler, uv));
	//return float4(rand.x * 512.0, rand.y * 512.0, rand.z * 512.0,0);
	return float4(0,0,0,0);
}

//float4 CopyTexturePS(float2)
//copies texture data from temporaryMap to whatevv!
float4 CopyTexturePS(in float2 uv : TEXCOORD0) : COLOR
{
	return tex2D(temporarySampler, uv);
}

//elapsed time since last call
float elapsedTime = 0.0f;
//float4 UpdatePositionsPS(float2)
//updates the positions based on elapsedTime since last update.
float4 UpdatePositionsPS(in float2 uv : TEXCOORD0) : COLOR
{
	float4 pos = tex2D(positionSampler, uv);
	float4 vel = tex2D(velocitySampler, uv);
	
	pos.xy += elapsedTime * vel;
	
	
	return pos;
}
float2 mousePos;
float mouseForce;
float4 UpdateVelocitiesPS(in float2 uv : TEXCOORD0) : COLOR
{
	float4 vel = tex2D(velocitySampler, uv);
	float4 pos = tex2D(positionSampler, uv);
	if (pos.y < -halfDimensions.y) 
	{
		vel.y *= -0.45;
		pos.y = -halfDimensions.y;
	}
	
	if (pos.y > halfDimensions.y)
	{
		vel.y *= -0.9;
		pos.y = halfDimensions.y;
	}
	
	if (pos.x < -halfDimensions.x)
	{
		vel.x *= -0.9;
		pos.x = -halfDimensions.x;
	}
	
	if (pos.x > halfDimensions.x)
	{
		vel.x *= -0.9;
		pos.x = halfDimensions.x;
	}
	
	float dx = (mousePos.x - pos.x);
	float dy = (mousePos.y - pos.y);
	
	float dist = sqrt(dx*dx + dy*dy);
	if (dist < 1.0) dist = 1;
	
	vel.y += (dy * mouseForce)/(dist*dist);
	vel.x += (dx * mouseForce)/(dist*dist);
	//vel.y+=(dy);
	
	vel.y -= 30.0 * elapsedTime;
	return vel;
}


technique ResetPositions
{
	pass P0
	{
		pixelShader = compile ps_3_0 ResetPositionPS();
	}
}

technique ResetVelocities
{
	pass P0
	{
		pixelShader = compile ps_3_0 ResetVelocitiesPS();
	}
}

technique CopyTexture
{
	pass P0
	{
		pixelShader = compile ps_3_0 CopyTexturePS();
	}
}

technique UpdatePositions
{
	pass P0
	{
		pixelShader = compile ps_3_0 UpdatePositionsPS();
	}
}

technique UpdateVelocities
{
	pass P0
	{
		pixelShader = compile ps_3_0 UpdateVelocitiesPS();
	}
}